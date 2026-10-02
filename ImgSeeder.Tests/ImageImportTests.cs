using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using ImgSeeder;
using OsLib;
using RaiImage;

namespace Iorg.Tests;

public sealed class ImageImportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RAIkeep-import-" + Guid.NewGuid().ToString("N"));
    private static readonly ImageImportLimits Limits = new(10_000_000, 20_000_000, 1000, TimeSpan.FromSeconds(5));
    private RaiPath Destination => new(Path.Combine(root, "images", "Nomsa"));

    [Fact]
    public async Task DefaultsHaveNoApplicationQuotaAndNestedMetadataIsSkipped()
    {
        var zip = Zip(new[] { ("nested/Nomsa_San_Diego_State_0001.jpg", "image"), ("metadata/info.json", "{}") });
        var result = await Import(zip, new ImageImportLimits());
        Assert.Equal("Completed", result.Status);
        Assert.Equal(1, result.Summary.Copied);
        Assert.Equal(1, result.Summary.Skipped);
    }

    [Theory]
    [InlineData("Photo.jpg", "photo.jpg")]
    [InlineData("folder", "folder/photo.jpg")]
    public async Task AmbiguousExtractionPathsRejectBeforeDestinationWrites(string first, string second)
    {
        var result = await Import(Zip(new[] { (first, "first"), (second, "second") }));
        Assert.Equal("Failed", result.Status);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task ConcertArchiveCopies193ImagesAndReplaysUnchanged()
    {
        var zip = Zip(Enumerable.Range(1, 193).Select(n => ($"concert/nomsa-concert-{n:D3}.jpg", "photo-" + n)).Append(("metadata/info.json", "{}")));
        var receipt = await Import(zip);
        Assert.Equal("Completed", receipt.Status);
        Assert.Equal(193, receipt.Summary.Copied);
        Assert.Equal(1, receipt.Summary.Skipped);
        Assert.All(receipt.Files.Where(f => f.Status == "Copied"), f =>
        {
            Assert.Equal("NomsaConcert", f.ItemId);
            Assert.StartsWith("NomsaCon/NomsaConce/", f.RelativePath);
            Assert.True(File.Exists(Path.Combine(Destination.FullPath, f.RelativePath!)));
        });
        var again = await Import(zip);
        Assert.Equal("Completed", again.Status);
        Assert.Equal(193, again.Summary.Unchanged);
        Assert.Equal(0, again.Summary.Copied);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(receipt));
        Assert.Equal("ImageImport", json.RootElement.GetProperty("Class").GetString());
        Assert.False(json.RootElement.TryGetProperty("Modified", out _));
        Assert.False(json.RootElement.TryGetProperty("ActivityId", out _));
    }

    [Theory]
    [InlineData("../../evil.jpg")]
    [InlineData("folder/../evil.jpg")]
    [InlineData("/absolute.jpg")]
    [InlineData("C:\\evil.jpg")]
    [InlineData("..\\evil.jpg")]
    public async Task UnsafeArchivePathRejectsBeforeAnyDestinationWrites(string name)
    {
        var receipt = await Import(Zip(new[] { ("valid-01.jpg", "photo"), (name, "evil") }));
        Assert.Equal("Failed", receipt.Status);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task SymlinkIsRejected()
    {
        var zip = Zip(new[] { ("link.jpg", "target") });
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update)) archive.Entries[0].ExternalAttributes = 0xA000 << 16;
        var receipt = await Import(zip);
        Assert.Equal("Failed", receipt.Status);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task AllQuotasAreCheckedBeforeWriting()
    {
        var zip = Zip(new[] { ("image-01.jpg", new string('x', 500)), ("image-02.jpg", "photo") });
        foreach (var limits in new[] { Limits with { MaxArchiveBytes = 1 }, Limits with { MaxExpandedBytes = 50 }, Limits with { MaxEntries = 1 } })
        {
            var receipt = await Import(zip, limits);
            Assert.Equal("Failed", receipt.Status);
            Assert.False(Destination.Exists());
        }
    }

    [Theory]
    [InlineData("{Placeholder}-01.jpg")]
    [InlineData("Photo<Pending-01.jpg")]
    public async Task InvalidImageIdentityRejectsBeforeNormalization(string name)
    {
        var receipt = await Import(Zip(new[] { ("a-valid-01.jpg", "photo"), (name, "bad") }));
        Assert.Equal("Failed", receipt.Status);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task NormalizedDuplicateDestinationsRejectWholeArchive()
    {
        var receipt = await Import(Zip(new[] { ("a/photo-01.jpg", "a"), ("b/photo-01.jpg", "b") }));
        Assert.Equal("Failed", receipt.Status);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task ExistingDifferentContentIsNeverOverwritten()
    {
        var zip = Zip(new[] { ("photo-01.jpg", "first") });
        var first = await Import(zip);
        var target = Path.Combine(Destination.FullPath, first.Files[0].RelativePath!);
        var second = await Import(Zip(new[] { ("photo-01.jpg", "second") }));
        Assert.Equal("Failed", second.Status);
        Assert.Equal("first", File.ReadAllText(target));
    }

    [Fact]
    public async Task RuntimeCopyFailureProducesTruthfulPartialReceipt()
    {
        // The second image's bucket cannot become a directory; the first remains copied.
        Directory.CreateDirectory(Destination.FullPath);
        File.WriteAllText(Path.Combine(Destination.FullPath, "Zzzzzzzz"), "blocker");
        var receipt = await Import(Zip(new[] { ("aaaa-photo-01.jpg", "first"), ("zzzzzzzz-photo-01.jpg", "second") }));
        Assert.Equal("Partial", receipt.Status);
        Assert.Equal(1, receipt.Summary.Copied);
        Assert.Equal(1, receipt.Summary.Failed);
        Assert.NotNull(receipt.Files.Single(f => f.Status == "Failed").Error);
    }

    [Fact]
    public async Task NoEligibleImagesFails()
    {
        var receipt = await Import(Zip(new[] { ("info.json", "{}") }));
        Assert.Equal("Failed", receipt.Status);
        Assert.Equal(1, receipt.Summary.Skipped);
        Assert.False(Destination.Exists());
    }

    [Fact]
    public async Task DirectHttpsDownloadImportsZipAndDoesNotExposeSignedQuery()
    {
        var zip = Zip(new[] { ("photo-01.jpg", "downloaded") });
        using var client = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(zip)) }));
        var receipt = await ImageImport.RunAsync(null, new Uri("https://images.example/photos.zip?secret=token"), "Import001", "Activity001", Destination, "Nomsa", Limits, downloadClient: client);
        Assert.Equal("Completed", receipt.Status);
        Assert.Equal("photos.zip", receipt.Source.Name);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(receipt));
    }

    [Fact]
    public async Task DownloadFailuresRedirectsAndOversizeResponsesWriteNoImages()
    {
        foreach (var response in new[]
        {
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>share page</html>") },
            new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://images.example/insecure.zip") } },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[100]) }
        })
        {
            using var client = new HttpClient(new FakeHandler(_ => response));
            var receipt = await ImageImport.RunAsync(null, new Uri("https://images.example/photos.zip?secret=token"), "Import001", null, Destination, "Nomsa", Limits with { MaxArchiveBytes = 50 }, downloadClient: client);
            Assert.Equal("Failed", receipt.Status);
            Assert.False(Destination.Exists());
            Assert.DoesNotContain("secret", JsonSerializer.Serialize(receipt));
        }
    }

    [Theory]
    [InlineData("{Import}", null)]
    [InlineData("Import", "<Activity>")]
    public async Task ReceiptIdentitiesAreValidatedBeforeInputAccess(string id, string? activity)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ImageImport.RunAsync("missing.zip", null, id, activity, Destination, "Nomsa", Limits));
        Assert.False(Destination.Exists());
    }

    private Task<ImageImportReceipt> Import(string zip, ImageImportLimits? limits = null) =>
        ImageImport.RunAsync(zip, null, "Import001", null, Destination, "Nomsa", limits ?? Limits);

    private string Zip(IEnumerable<(string name, string content)> entries)
    {
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(Encoding.UTF8.GetBytes(content));
        }
        return file;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(respond(request));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
