using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
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
    private RaiPath Destination => new(Path.Combine(root, "images", "Customer"));

    [Fact]
    public async Task DefaultsHaveNoApplicationQuotaAndNestedMetadataIsSkipped()
    {
        var zip = Zip(new[] { ("nested/Customer_Order_Sheet_0001.jpg", "image"), ("metadata/info.json", "{}") });
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
    public async Task OrderArchiveCopies193ImagesAndReplaysUnchanged()
    {
        var zip = Zip(Enumerable.Range(1, 193).Select(n => ($"orders/customer-order-{n:D3}.jpg", "photo-" + n)).Append(("metadata/info.json", "{}")));
        var receipt = await Import(zip);
        Assert.Equal("Completed", receipt.Status);
        Assert.Equal(193, receipt.Summary.Copied);
        Assert.Equal(1, receipt.Summary.Skipped);
        Assert.All(receipt.Files.Where(f => f.Status == "Copied"), f =>
        {
            Assert.Equal("CustomerOrder", f.ItemId);
            Assert.StartsWith("Customer/CustomerOr/", f.RelativePath);
            Assert.True(File.Exists(Path.Combine(Destination.FullPath, f.RelativePath!)));
        });
        var again = await Import(zip);
        Assert.Equal("Completed", again.Status);
        Assert.Equal(193, again.Summary.Unchanged);
        Assert.Equal(0, again.Summary.Copied);
        Assert.Equal(0, again.Summary.Updated);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(receipt));
        Assert.Equal("ImageImport", json.RootElement.GetProperty("Class").GetString());
        Assert.False(json.RootElement.TryGetProperty("Modified", out _));
        Assert.False(json.RootElement.TryGetProperty("ActivityId", out _));
    }

    [Fact]
    public async Task CleanBatchEmitsCompactReceiptBelow500BytesWithReconstructiblePaths()
    {
        var receipt = await Import(Zip(Enumerable.Range(1, 193).Select(n => ($"orders/customer-order-26-{n}.jpg", "photo-" + n))));
        Assert.Equal(193, receipt.Summary.Copied);
        var serialized = JsonSerializer.Serialize(receipt);
        Assert.True(Encoding.UTF8.GetByteCount(serialized) < 500, serialized);
        using var json = JsonDocument.Parse(serialized);
        var result = json.RootElement;
		Assert.Equal(3, result.GetProperty("ReceiptVersion").GetInt32());
		Assert.Equal("CustomerOrder26", result.GetProperty("BaseItemId").GetString());
        Assert.Equal("ItemIdTree8x2", result.GetProperty("PathConvention").GetString());
        Assert.Equal("Structured", result.GetProperty("NamingConvention").GetString());
        Assert.Equal(1, result.GetProperty("Range").GetProperty("Start").GetInt32());
        Assert.Equal(193, result.GetProperty("Range").GetProperty("End").GetInt32());
        Assert.Equal(193, result.GetProperty("Range").GetProperty("Count").GetInt32());
        Assert.False(result.TryGetProperty("Files", out _));
        Assert.False(result.TryGetProperty("Exceptions", out _));
        Assert.False(result.TryGetProperty("Status", out _));
        foreach (var number in Enumerable.Range(1, 193))
        {
			var target = new ImageTreeFile(Destination, "CustomerOrder26", string.Empty, "jpg", PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured) { ImageNumber = number };
            Assert.True(target.Exists(), target.FullName);
        }
        var repeat = await Import(Zip(Enumerable.Range(1, 193).Select(n => ($"orders/customer-order-26-{n}.jpg", "photo-" + n))));
		Assert.Equal(3, repeat.ReceiptVersion);
        Assert.Equal(193, repeat.Summary.Unchanged);
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
    public async Task ExistingDifferentContentIsUpdatedInPlace()
    {
        var zip = Zip(new[] { ("photo-01.jpg", "first") });
        var first = await Import(zip);
        var target = Path.Combine(Destination.FullPath, first.Files[0].RelativePath!);
        var second = await Import(Zip(new[] { ("photo-01.jpg", "second") }));
        Assert.Equal("Completed", second.Status);
        Assert.Equal(1, second.Summary.Updated);
        Assert.Equal(0, second.Summary.Copied);
        Assert.Equal("second", File.ReadAllText(target));
    }

    [Fact]
    public async Task SingleVideoFileCopiesThenReportsUnchangedAndUpdated()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "Server-Clip-001.mp4");
        await File.WriteAllTextAsync(source, "first", TestContext.Current.CancellationToken);

        var first = await ImageImport.RunAsync(source, null, "Import001", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken, itemTreePrefix: "SampleApp/Image/Customer");
        Assert.Equal("Completed", first.Status);
        Assert.Equal("File", first.Source.Kind);
        Assert.Equal(1, first.Summary.Copied);
        var row = Assert.Single(first.Files);
        Assert.Equal("mp4", row.Ext);
        Assert.Equal(5, row.SizeBytes);
        Assert.Equal("SampleApp/Image/Customer/ServerCl/ServerClip/ServerClip_001.mp4", row.ItemTreePath);
        var destination = Path.Combine(Destination.FullPath, row.RelativePath!);
        Assert.True(File.Exists(destination));

        var unchanged = await ImageImport.RunAsync(source, null, "Import002", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, unchanged.Summary.Unchanged);

        await File.WriteAllTextAsync(source, "second", TestContext.Current.CancellationToken);
        var updated = await ImageImport.RunAsync(source, null, "Import003", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, updated.Summary.Updated);
        Assert.Equal("second", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(updated));
        var file = json.RootElement.GetProperty("Files")[0];
        Assert.Equal("mp4", file.GetProperty("Ext").GetString());
        Assert.Equal("Updated", file.GetProperty("Status").GetString());
        Assert.Equal(6, file.GetProperty("SizeBytes").GetInt64());
        Assert.EndsWith("ServerClip_001.mp4", file.GetProperty("ItemTreePath").GetString());
    }

    [Fact]
    public async Task MoveDeletesSingleScratchFileOnlyAfterVerifiedCopy()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "Server-Clip-002.mov");
        await File.WriteAllTextAsync(source, "video bytes", TestContext.Current.CancellationToken);
        var expectedHash = SHA256.HashData(await File.ReadAllBytesAsync(source, TestContext.Current.CancellationToken));

        var receipt = await ImageImport.RunAsync(source, null, "Import001", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken, moveSource: true);

        Assert.Equal("Completed", receipt.Status);
        Assert.Equal(1, receipt.Summary.Copied);
        Assert.False(File.Exists(source));
        var target = Path.Combine(Destination.FullPath, Assert.Single(receipt.Files).RelativePath!);
        Assert.Equal(expectedHash, SHA256.HashData(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task FailedGuardedUpdateRetainsScratchSource()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "Server-Clip-003.mp4");
        await File.WriteAllTextAsync(source, "original", TestContext.Current.CancellationToken);
        var first = await ImageImport.RunAsync(source, null, "Import001", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Completed", first.Status);
        await File.WriteAllTextAsync(source, "replacement", TestContext.Current.CancellationToken);

        var guarded = await ImageImport.RunAsync(source, null, "Import002", null, Destination, "Customer", Limits,
            cancellationToken: TestContext.Current.CancellationToken, noOverwrite: true, moveSource: true);

        Assert.Equal("Failed", guarded.Status);
        Assert.True(File.Exists(source));
        Assert.Equal("original", await File.ReadAllTextAsync(
            Path.Combine(Destination.FullPath, first.Files.Single().RelativePath!), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MixedArchiveImportsStillImageAndVideo()
    {
        var receipt = await Import(Zip(new[] { ("Order-Sheet-001.jpg", "still"), ("Server-Clip-001.webm", "video") }));
        Assert.Equal("Completed", receipt.Status);
        Assert.Equal(2, receipt.Summary.Copied);
        Assert.Equal(new[] { "jpg", "webm" }, receipt.Files.Select(file => file.Ext).OrderBy(ext => ext).ToArray());
        Assert.All(receipt.Files, file => Assert.True(File.Exists(Path.Combine(Destination.FullPath, file.RelativePath!))));
    }

    [Fact]
    public async Task ReimportChangedImageInvalidatesOnlyItsManagedDerivatives()
    {
        var original = Zip(Enumerable.Range(1, 3).Select(n => ($"Order-Sheet-{n:D3}.jpg", $"old-{n}")));
        var first = await Import(original);
        Assert.Equal(3, first.Summary.Copied);
        var source = Path.Combine(Destination.FullPath, first.Files.Single(f => f.ImageNumber == 2).RelativePath!);
        var bucket = Path.GetDirectoryName(source)!;
        var staleThumb = Path.Combine(bucket, "OrderSheet_002_Thumb.webp");
        var staleSmall = Path.Combine(bucket, "OrderSheet_002_Small.jpg");
        var otherThumb = Path.Combine(bucket, "OrderSheet_001_Thumb.webp");
        var otherSource = Path.Combine(Destination.FullPath, first.Files.Single(f => f.ImageNumber == 3).RelativePath!);
        var unadornedWebp = Path.Combine(bucket, "OrderSheet_002.webp");
        var diagram = Path.Combine(bucket, "OrderSheet_002.raid");
        var sibling = Path.Combine(bucket, "OrderSheetExtra_002_Thumb.webp");
        foreach (var file in new[] { staleThumb, staleSmall, otherThumb, unadornedWebp, diagram, sibling }) File.WriteAllText(file, "retain unless derived from 2");

        var changed = await Import(Zip(Enumerable.Range(1, 3).Select(n => ($"Order-Sheet-{n:D3}.jpg", n == 2 ? "new-2" : $"old-{n}"))));

        Assert.Equal("Completed", changed.Status);
        Assert.Equal(0, changed.Summary.Copied);
        Assert.Equal(1, changed.Summary.Updated);
        Assert.Equal(2, changed.Summary.Unchanged);
        Assert.Equal("new-2", File.ReadAllText(source));
        Assert.Equal("old-3", File.ReadAllText(otherSource));
        Assert.False(File.Exists(staleThumb));
        Assert.False(File.Exists(staleSmall));
        Assert.All(new[] { otherThumb, unadornedWebp, diagram, sibling }, file => Assert.True(File.Exists(file), file));
        Assert.Equal(1, changed.Range!.Updated);
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(changed));
        Assert.Equal(1, serialized.RootElement.GetProperty("Range").GetProperty("Updated").GetInt32());
        Assert.Equal(1, serialized.RootElement.GetProperty("Summary").GetProperty("Updated").GetInt32());
    }

    [Fact]
    public async Task ExpandedArchiveAddsOnlyNewImagesAndPartialArchivePreservesOthers()
    {
        await Import(Zip(Enumerable.Range(1, 3).Select(n => ($"Order-Sheet-{n:D3}.jpg", $"photo-{n}"))));
        var expanded = await Import(Zip(Enumerable.Range(1, 5).Select(n => ($"Order-Sheet-{n:D3}.jpg", $"photo-{n}"))));
        Assert.Equal(2, expanded.Summary.Copied);
        Assert.Equal(3, expanded.Summary.Unchanged);
        Assert.Equal(0, expanded.Summary.Updated);

        var partial = await Import(Zip(new[] { ("Order-Sheet-002.jpg", "revised-2") }));
        Assert.Equal(1, partial.Summary.Updated);
        foreach (var n in new[] { 1, 3, 4, 5 })
        {
            var image = new ImageTreeFile(Destination, "OrderSheet", string.Empty, "jpg", PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured) { ImageNumber = n };
            Assert.Equal($"photo-{n}", File.ReadAllText(image.FullName));
        }
    }

    [Fact]
    public async Task NoOverwriteRejectsWholeBatchBeforeWriting()
    {
        var first = await Import(Zip(new[] { ("Order-Sheet-001.jpg", "original") }));
        var existing = Path.Combine(Destination.FullPath, first.Files[0].RelativePath!);
        var incoming = Zip(new[] { ("Order-Sheet-001.jpg", "different"), ("Order-Sheet-002.jpg", "new") });
        var result = await ImageImport.RunAsync(incoming, null, "Import001", null, Destination, "Customer", Limits,
			cancellationToken: TestContext.Current.CancellationToken, noOverwrite: true);
        Assert.Equal("Failed", result.Status);
        Assert.Equal("original", File.ReadAllText(existing));
        var added = new ImageTreeFile(Destination, "OrderSheet", string.Empty, "jpg", PathConventionType.ItemIdTree8x2, ImageNamingConvention.Structured) { ImageNumber = 2 };
        Assert.False(added.Exists());
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
        var receipt = await ImageImport.RunAsync(null, new Uri("https://images.example/photos.zip?secret=token"), "Import001", "Activity001", Destination, "Customer", Limits, downloadClient: client);
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
            var receipt = await ImageImport.RunAsync(null, new Uri("https://images.example/photos.zip?secret=token"), "Import001", null, Destination, "Customer", Limits with { MaxArchiveBytes = 50 }, downloadClient: client);
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
        await Assert.ThrowsAsync<ArgumentException>(() => ImageImport.RunAsync("missing.zip", null, id, activity, Destination, "Customer", Limits));
        Assert.False(Destination.Exists());
    }

    private Task<ImageImportReceipt> Import(string zip, ImageImportLimits? limits = null) =>
        ImageImport.RunAsync(zip, null, "Import001", null, Destination, "Customer", limits ?? Limits);

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
