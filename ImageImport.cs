using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using JsonPit;
using OsLib;
using RaiImage;

namespace ImgSeeder;

public sealed record ImageImportLimits(long? MaxArchiveBytes = null, long? MaxExpandedBytes = null, int? MaxEntries = null, TimeSpan? DownloadTimeout = null)
{
    public void Validate()
    {
        if (MaxArchiveBytes <= 0 || MaxExpandedBytes <= 0 || MaxEntries <= 0 || DownloadTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Import resource limits must be positive.");
    }
}

public sealed class ImageImportReceipt
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    public string Class => "ImageImport";
    public int ReceiptVersion => 2;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaseItemId { get; private set; }
    [JsonIgnore]
    public PathConventionType PathMode { get; init; } = PathConventionType.ItemIdTree8x2;
    [JsonIgnore]
    public ImageNamingConvention NamingMode { get; init; } = ImageNamingConvention.Structured;
    [JsonIgnore]
    public bool IncludeExif { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PathConvention => Range is null ? null : PathMode.ToString();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NamingConvention => Range is null ? null : NamingMode.ToString();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageImportRange? Range { get; private set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ImageImportNamedRange>? Ranges { get; private set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ImageImportEntry>? Exceptions { get; private set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActivityId { get; init; }
    // Operational result for library callers and CLI exit status; never persisted as Activity state.
    [JsonIgnore]
    public string Status { get; internal set; } = "Failed";
    public required string Tenant { get; init; }
    public required ImageImportSource Source { get; init; }
    public ImageImportSummary Summary => new(
        Files.Count(f => f.Status == "Copied"), Files.Count(f => f.Status == "Unchanged"),
        Files.Count(f => f.Status == "Skipped"), Files.Count(f => f.Status == "Failed"));
    // Retained in memory for diagnostics and existing library callers.
    [JsonIgnore]
    public List<ImageImportEntry> Files { get; } = [];
    [JsonPropertyName("Files")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ImageImportEntry>? DetailedFiles => IncludeExif ? Files : null;

    internal void Compact()
    {
        var exceptions = Files.Where(f => f.Status is "Failed" or "Skipped").ToList();
        var ranges = new List<ImageImportNamedRange>();
        foreach (var group in Files.Where(f => f.Status is "Copied" or "Unchanged")
            .GroupBy(f => (f.ItemId, f.Ext)).OrderBy(g => g.Key.ItemId, StringComparer.Ordinal).ThenBy(g => g.Key.Ext, StringComparer.Ordinal))
        {
            var entries = group.OrderBy(f => f.ImageNumber).ToArray();
            for (var start = 0; start < entries.Length;)
            {
                var end = start;
                while (end + 1 < entries.Length && entries[end + 1].ImageNumber == (long?)entries[end].ImageNumber + 1) end++;
                if (end > start && entries[start].ImageNumber is >= 0)
                    ranges.Add(new(group.Key.ItemId!, PathMode.ToString(), NamingMode.ToString(),
                        entries[start].ImageNumber!.Value, entries[end].ImageNumber!.Value, end - start + 1, group.Key.Ext!));
                else
                {
                    entries[start].Reason ??= "Singleton image outside a contiguous range.";
                    exceptions.Add(entries[start]);
                }
                start = end + 1;
            }
        }
        if (ranges.Count == 1)
        {
            var range = ranges[0];
            BaseItemId = range.BaseItemId;
            Range = new(range.Start, range.End, range.Count, range.Ext);
        }
        else if (ranges.Count > 1) Ranges = ranges;
        if (exceptions.Count > 0) Exceptions = exceptions.OrderBy(e => e.SourceEntry, StringComparer.Ordinal).ToArray();
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; internal set; }
}
public sealed record ImageImportNamedRange(string BaseItemId, string PathConvention, string NamingConvention, int Start, int End, int Count, string Ext);
public sealed record ImageImportRange(int Start, int End, int Count, string Ext);
public sealed record ImageImportSource(string Kind, string Name);
public sealed record ImageImportSummary(int Copied, int Unchanged, int Skipped, int Failed);
public sealed class ImageImportEntry
{
    public required string SourceEntry { get; init; }
    [JsonIgnore]
    public string? Ext { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ItemId { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ImageNumber { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RelativePath { get; internal set; }
    public string Status { get; internal set; } = "Pending";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExifMetadata? Exif { get; internal set; }
}

/// <summary>CR049: preflight input in full, then copy directly to final ImageTree paths.</summary>
public static class ImageImport
{
    private sealed record Candidate(ImageImportEntry Entry, ImageTreeFile Destination, Func<Stream> Open, long Length, byte[] Hash);
    private static readonly HashSet<string> Extensions = ImageTypes.Default.Array
        .Select(e => "." + e.Trim().TrimStart('.')).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static Task<ImageImportReceipt> RunAsync(
        string? source, Uri? sourceUrl, string? importId, string? activityId,
        RaiPath destination, string tenant, ImageImportLimits limits,
        PathConventionType pathConvention = PathConventionType.ItemIdTree8x2,
        ImageNamingConvention namingConvention = ImageNamingConvention.Structured,
        HttpClient? downloadClient = null, CancellationToken cancellationToken = default, string? exif = null)
        // Archive metadata inspection and filename parsing are synchronous work too.
        // Dispatch the complete operation so a UI caller can safely await this API.
        => Task.Run(() => RunCoreAsync(source, sourceUrl, importId, activityId, destination,
            tenant, limits, pathConvention, namingConvention, downloadClient, cancellationToken, exif), cancellationToken);

    private static async Task<ImageImportReceipt> RunCoreAsync(
        string? source, Uri? sourceUrl, string? importId, string? activityId,
        RaiPath destination, string tenant, ImageImportLimits limits,
        PathConventionType pathConvention, ImageNamingConvention namingConvention,
        HttpClient? downloadClient, CancellationToken cancellationToken, string? exif)
    {
        if (importId is not null) PitItem.ValidateLiveId(importId);
        if (activityId is not null) PitItem.ValidateLiveId(activityId);
        limits.Validate();
        if (exif is not null) ImageExif.ValidateSelector(exif);
        if ((source is null) == (sourceUrl is null)) throw new ArgumentException("Specify exactly one of --source or --source-url.");
        if (sourceUrl is not null) ValidateUrl(sourceUrl);
        var zipInput = sourceUrl is not null || string.Equals(System.IO.Path.GetExtension(source), ".zip", StringComparison.OrdinalIgnoreCase);
        var receipt = new ImageImportReceipt
        {
            Id = importId, ActivityId = activityId, Tenant = tenant,
            PathMode = pathConvention, NamingMode = namingConvention, IncludeExif = exif is not null,
            Source = new(zipInput ? "Zip" : "Directory", sourceUrl is null
                ? System.IO.Path.GetFileName(source!.TrimEnd('/', '\\'))
                : System.IO.Path.GetFileName(sourceUrl.AbsolutePath))
        };
        RaiPath? workspace = null;
        try
        {
            if (sourceUrl is not null)
            {
                workspace = Os.TempDir / "RAIkeep" / "image-import" / Guid.NewGuid().ToString("N");
                var downloaded = new RaiFile(workspace, "input", "zip");
                await DownloadAsync(sourceUrl, downloaded, limits, downloadClient, cancellationToken);
                source = downloaded.FullName;
            }
            if (zipInput)
            {
                using var input = new RaiFile(source!).OpenRead();
                if (input.Length > limits.MaxArchiveBytes) throw new InvalidDataException("Archive exceeds --max-archive-bytes.");
                using var archive = new ZipArchive(input, ZipArchiveMode.Read);
                if (archive.Entries.Count > limits.MaxEntries) throw new InvalidDataException("Archive exceeds --max-entries.");
                long expanded = 0;
                var entryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                // Validate every path and declared size before decompressing or creating output.
                foreach (var entry in archive.Entries)
                {
                    ValidateEntry(entry);
                    var path = entry.FullName.TrimEnd('/').Normalize();
                    if (!entryPaths.Add(path)) throw new InvalidDataException("Duplicate archive path: " + entry.FullName);
                    if (!entry.FullName.EndsWith('/')) filePaths.Add(path);
                    if (entry.Length > limits.MaxExpandedBytes - expanded) throw new InvalidDataException("Archive exceeds --max-expanded-bytes.");
                    expanded = checked(expanded + entry.Length);
                }
                foreach (var path in entryPaths)
                    for (var index = path.IndexOf('/'); index >= 0; index = path.IndexOf('/', index + 1))
                        if (filePaths.Contains(path[..index])) throw new InvalidDataException("Archive path is both a file and a directory: " + path[..index]);

                workspace ??= Os.TempDir / "RAIkeep" / "image-import" / Guid.NewGuid().ToString("N");
                var extracted = workspace / "extracted";
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    var result = await new UnzipCommand().ExtractAsync(new RaiFile(source!), extracted, cancellationToken);
                    if (!result.Succeeded)
                        throw new IOException($"unzip failed (exit {result.ExitCode}): {result.Output.Trim()}");
                    // Verify that extraction produced every ordinary entry at its expected path.
                    // CRC/decompression errors are reported by unzip's exit status.
                    foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith('/')))
                    {
                        using var extractedFile = new RaiFile(System.IO.Path.Combine(extracted.FullPath, entry.FullName)).OpenRead();
                        if (extractedFile.Length != entry.Length) throw new InvalidDataException("Extracted size differs from ZIP metadata: " + entry.FullName);
                    }
                }
                else
                {
                    foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith('/')))
                    {
                        using var data = new LimitedReadStream(entry.Open(), entry.Length);
                        await new RaiFile(System.IO.Path.Combine(extracted.FullPath, entry.FullName)).WriteNewFromAsync(data, cancellationToken);
                    }
                }
                var plan = new List<Candidate>();
                foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var row = new ImageImportEntry { SourceEntry = entry.FullName };
                    receipt.Files.Add(row);
                    var staged = new RaiFile(System.IO.Path.Combine(extracted.FullPath, entry.FullName));
                    if (entry.FullName.EndsWith('/') || !Extensions.Contains(System.IO.Path.GetExtension(entry.Name)) || entry.FullName.StartsWith("__MACOSX/", StringComparison.Ordinal) || entry.Name.StartsWith("._", StringComparison.Ordinal))
                    {
                        row.Status = "Skipped";
                        row.Reason = "Directory, metadata, or unsupported image type.";
                        continue;
                    }
                    plan.Add(await PlanAsync(row, staged, entry.Length, destination, pathConvention, namingConvention, cancellationToken, exif));
                }
                await ApplyAsync(plan, receipt, cancellationToken);
            }
            else
            {
                var root = new RaiPath(source!);
                if (!root.Exists()) throw new ArgumentException("Source directory does not exist.");
                var files = root.EnumerateFiles("*").OrderBy(f => f.NameWithExtension, StringComparer.Ordinal).ToList();
                if (files.Count > limits.MaxEntries) throw new InvalidDataException("Source exceeds --max-entries.");
                var plan = new List<Candidate>();
                long expanded = 0;
                foreach (var file in files)
                {
                    var row = new ImageImportEntry { SourceEntry = file.NameWithExtension };
                    receipt.Files.Add(row);
                    if (!Extensions.Contains("." + file.Ext.TrimStart('.')))
                    {
                        row.Status = "Skipped"; row.Reason = "Unsupported image type."; continue;
                    }
                    using var input = file.OpenRead();
                    if (input.Length > limits.MaxExpandedBytes - expanded) throw new InvalidDataException("Source exceeds --max-expanded-bytes.");
                    expanded += input.Length;
                    plan.Add(await PlanAsync(row, file, input.Length, destination, pathConvention, namingConvention, cancellationToken, exif));
                }
                await ApplyAsync(plan, receipt, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or ArgumentException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or OverflowException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // Download errors can include signed URLs. Once download has completed,
            // local extraction/copy diagnostics contain only the temporary pathname.
            receipt.Error = sourceUrl is null || source is not null ? ex.Message : SafeDownloadError(ex);
            foreach (var row in receipt.Files.Where(f => f.Status == "Pending"))
            {
                row.Status = "Failed"; row.Error = "Import preflight failed; no copy attempted.";
            }
            receipt.Status = "Failed";
        }
        finally
        {
            if (workspace?.Exists() == true)
            {
                try { workspace.rmdir(depth: int.MaxValue, deleteFiles: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    receipt.Error = "Temporary import cleanup failed; " + (receipt.Error ?? "inspect the OS temporary import workspace.");
                    if (receipt.Status == "Completed") receipt.Status = "Partial";
                }
            }
        }
        receipt.Compact();
        return receipt;
    }

    private static async Task<Candidate> PlanAsync(ImageImportEntry row, RaiFile source, long length,
        RaiPath root, PathConventionType pathConvention, ImageNamingConvention namingConvention, CancellationToken ct, string? exif)
    {
        // Inspect before EasyFileName can erase a template marker.
        PitItem.ValidateLiveId(source.Name);
        var normalized = new ImageFile(ImageFile.EasyFileName(source.FullName), namingConvention);
        PitItem.ValidateLiveId(normalized.ItemId);
        var target = new ImageTreeFile(root, normalized.ItemId, string.Empty, normalized.Ext, pathConvention, namingConvention)
        { ImageNumber = normalized.ImageNumber };
        row.ItemId = normalized.ItemId;
        row.Ext = normalized.Ext;
        row.ImageNumber = normalized.ImageNumber;
        row.RelativePath = System.IO.Path.GetRelativePath(root.FullPath, target.FullName).Replace('\\', '/');
        if (row.RelativePath.StartsWith("../", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(row.RelativePath))
            throw new InvalidDataException("Image destination is outside the tenant root.");
        if (exif is not null) row.Exif = await ImageExif.ReadMetadataAsync(source, exif, ct);
        using var input = new LimitedReadStream(source.OpenRead(), length);
        var hash = await SHA256.HashDataAsync(input, ct);
        return new Candidate(row, target, source.OpenRead, length, hash);
    }

    private static async Task ApplyAsync(List<Candidate> plan, ImageImportReceipt receipt, CancellationToken ct)
    {
        if (plan.Count == 0) throw new InvalidDataException("Source contained 0 eligible images.");
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // No destination creation until every collision has been checked.
        foreach (var candidate in plan)
        {
            if (!destinations.Add(candidate.Destination.FullName)) throw new InvalidDataException("Multiple source images resolve to the same destination: " + candidate.Entry.RelativePath);
            if (candidate.Destination.Exists() && !await MatchesAsync(candidate, ct))
                throw new InvalidDataException("Destination contains different content: " + candidate.Entry.RelativePath);
        }
        foreach (var candidate in plan)
        {
            try
            {
                if (candidate.Destination.Exists())
                {
                    if (!await MatchesAsync(candidate, ct)) throw new IOException("Destination changed after preflight; existing file preserved.");
                    candidate.Entry.Status = "Unchanged";
                }
                else
                {
                    using var input = new LimitedReadStream(candidate.Open(), candidate.Length);
                    await candidate.Destination.WriteNewFromAsync(input, ct);
                    if (!await MatchesAsync(candidate, ct)) throw new IOException("Copied image failed content verification.");
                    candidate.Entry.Status = "Copied";
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                candidate.Entry.Status = "Failed";
                candidate.Entry.Error = ex.Message;
            }
        }
        var summary = receipt.Summary;
        receipt.Status = summary.Failed == 0 ? "Completed" : summary.Copied + summary.Unchanged > 0 ? "Partial" : "Failed";
    }

    private static async Task<bool> MatchesAsync(Candidate candidate, CancellationToken ct)
    {
        using var existing = candidate.Destination.OpenRead();
        if (existing.Length != candidate.Length) return false;
        var hash = await SHA256.HashDataAsync(existing, ct);
        return candidate.Hash.AsSpan().SequenceEqual(hash);
    }

    private static void ValidateEntry(ZipArchiveEntry entry)
    {
        var path = entry.FullName.Replace('\\', '/');
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (path.StartsWith('/') || path.Contains(':') || entry.FullName.Contains('\\') ||
            path.TrimEnd('/').Split('/').Any(segment => segment is ".." or "." or "") || path.Contains('\0') ||
            (unixType != 0 && unixType != 0x8000 && unixType != 0x4000) || (entry.ExternalAttributes & 0x400) != 0)
            throw new InvalidDataException("Unsafe ZIP entry: " + entry.FullName);
    }

    private static void ValidateUrl(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(url.UserInfo))
            throw new ArgumentException("--source-url requires a direct HTTPS URL without embedded credentials.");
    }

    private static string SafeDownloadError(Exception ex) => ex switch
    {
        OperationCanceledException => "Import/download cancelled or exceeded its timeout.",
        HttpRequestException => "HTTPS ZIP download failed; supply a direct download URL or a local ZIP.",
        _ => "ZIP import failed: " + (ex is InvalidDataException ? ex.Message : "unable to read or copy the input; check permissions and archive contents.")
    };

    private static async Task DownloadAsync(Uri url, RaiFile target, ImageImportLimits limits, HttpClient? suppliedClient, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (limits.DownloadTimeout is { } timeout) deadline.CancelAfter(timeout);
        using var ownedClient = suppliedClient is null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan } : null;
        var client = suppliedClient ?? ownedClient!;
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            ValidateUrl(url);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location ?? throw new HttpRequestException("Missing redirect location.");
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > limits.MaxArchiveBytes) throw new InvalidDataException("Download exceeds --max-archive-bytes.");
            using var input = new LimitedReadStream(await response.Content.ReadAsStreamAsync(deadline.Token), limits.MaxArchiveBytes ?? long.MaxValue, exact: false);
            await target.WriteNewFromAsync(input, deadline.Token);
            return;
        }
        throw new HttpRequestException("Too many redirects.");
    }

    /// <summary>Bounds actual bytes read even when archive/HTTP length metadata is dishonest.</summary>
    private sealed class LimitedReadStream(Stream inner, long limit, bool exact = true) : Stream
    {
        private long consumed;
        private int Count(int count)
        {
            if (count == 0 && exact && consumed != limit) throw new InvalidDataException("Input ended before its declared size.");
            if (count > limit - consumed) throw new InvalidDataException("Input exceeds its declared size or resource quota.");
            consumed += count;
            return count;
        }
        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => Count(await inner.ReadAsync(buffer, ct));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => consumed; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
