using System.IO.Compression;
using System.Text.Json;
using OsLib;

namespace Iorg.Tests;

public sealed class ImportCliTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "RAIkeep-import-cli-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Photo = Convert.FromBase64String("/9j/4QBoRXhpZgAASUkqAAgAAAACADIBAgAUAAAAOAAAAGmHBAABAAAAJgAAAAAAAAABAAOQAgAUAAAATAAAAAAAAAAyMDI2OjA5OjI4IDIwOjI0OjMxADIwMjY6MDk6MjggMTc6NDA6MzEA/+AAEEpGSUYAAQEAAAEAAQAA/9sAQwADAgICAgIDAgICAwMDAwQGBAQEBAQIBgYFBgkICgoJCAkJCgwPDAoLDgsJCQ0RDQ4PEBAREAoMEhMSEBMPEBAQ/8AACwgAAgACAQERAP/EABQAAQAAAAAAAAAAAAAAAAAAAAn/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAA/ACqf/9k=");

    [Fact]
    public void ZipReceiptUsesExistingRaiImageNamingAndSupportsExifOnBothVerbs()
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "photos.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("nested/Customer_Order_Sheet_0001.jpg").Open()) stream.Write(Photo);
        var images = Path.Combine(root, "images");
        var import = Run("organize", "--source", zipPath, "--root", images, "--tenant", "Customer", "--import-id", "Photos001", "--json", "--debug", "--exif", "DateTimeOriginal,DateTime");
        Assert.Equal(0, import.ExitCode);
        using var receipt = JsonDocument.Parse(import.StandardOutput);
        var row = receipt.RootElement.GetProperty("Files")[0];
        Assert.Equal("CustomerOrderSheet", row.GetProperty("ItemId").GetString());
        Assert.Equal(1, row.GetProperty("ImageNumber").GetInt32());
        Assert.Equal("Customer/CustomerOr/CustomerOrderSheet_001.jpg", row.GetProperty("RelativePath").GetString());
        Assert.Equal("2026:09:28 17:40:31", row.GetProperty("Exif").GetProperty("Unconverted").GetProperty("DateTimeOriginal").GetString());
        Assert.Contains("OffsetTimeOriginal", import.StandardError);
        var list = Run("list", "CustomerOrderSheet*", "--root", images, "--tenant", "Customer", "--exif", "DateTimeOriginal", "--json");
        Assert.Equal(0, list.ExitCode);
        using var metadata = JsonDocument.Parse(list.StandardOutput);
        Assert.Equal(row.GetProperty("Exif").GetProperty("Unconverted").GetProperty("DateTimeOriginal").GetString(),
            metadata.RootElement[0].GetProperty("Exif").GetProperty("Unconverted").GetProperty("DateTimeOriginal").GetString());
        var repeat = Run("organize", "--source", zipPath, "--root", images, "--tenant", "Customer", "--import-id", "Photos001", "--json");
        Assert.Equal(0, repeat.ExitCode);
        using var again = JsonDocument.Parse(repeat.StandardOutput);
        Assert.Equal(1, again.RootElement.GetProperty("Summary").GetProperty("Unchanged").GetInt32());
    }

    [Fact]
    public void InvalidSourceProducesFailedJsonAndStderrWithoutImages()
    {
        var run = Run("organize", "--source", Path.Combine(root, "missing.zip"), "--root", Path.Combine(root, "images"), "--tenant", "Customer", "--import-id", "Photos001", "--json");
        Assert.Equal(1, run.ExitCode);
        using var receipt = JsonDocument.Parse(run.StandardOutput);
        Assert.False(receipt.RootElement.TryGetProperty("Status", out _));
        Assert.NotEmpty(receipt.RootElement.GetProperty("Error").GetString()!);
        Assert.NotEmpty(run.StandardError);
        Assert.False(Directory.Exists(Path.Combine(root, "images")));
    }

    [Fact]
    public void SingleVideoFileWritesMachineCleanReceiptAndCanMoveScratchSource()
    {
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "Server-Clip-001.mp4");
        File.WriteAllText(source, "video bytes");
        var images = Path.Combine(root, "images");

        var run = Run("organize", "--source", source, "--root", images,
            "--tenant", "Customer", "--move", "--json");

        Assert.Equal(0, run.ExitCode);
        Assert.False(File.Exists(source));
        using var receipt = JsonDocument.Parse(run.StandardOutput);
        Assert.Equal(3, receipt.RootElement.GetProperty("ReceiptVersion").GetInt32());
        Assert.Equal(1, receipt.RootElement.GetProperty("Summary").GetProperty("Copied").GetInt32());
		Assert.False(receipt.RootElement.TryGetProperty("Exceptions", out _));
        var entry = receipt.RootElement.GetProperty("Files")[0];
        Assert.Equal("mp4", entry.GetProperty("Ext").GetString());
		Assert.Equal("Customer/ServerCl/ServerClip/ServerClip_001.mp4", entry.GetProperty("ItemTreePath").GetString());
        Assert.Equal(11, entry.GetProperty("SizeBytes").GetInt64());
        Assert.Equal("Copied", entry.GetProperty("Status").GetString());
        var listed = Run("list", "*.mp4", "--root", images, "--tenant", "Customer", "--json");
        Assert.Equal(0, listed.ExitCode);
        Assert.Equal("ServerClip_001.mp4", JsonDocument.Parse(listed.StandardOutput).RootElement[0].GetString());
    }

    [Fact]
    public void MoveRejectsArchiveAndPreservesSource()
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "archive.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("Server-Clip-001.mp4").Open()) stream.WriteByte(1);

        var run = Run("organize", "--source", zipPath, "--root", Path.Combine(root, "images"),
            "--tenant", "Customer", "--move", "--json");

        Assert.Equal(1, run.ExitCode);
        Assert.Empty(run.StandardOutput);
        Assert.Contains("--move is only supported for single-file sources", run.StandardError);
        Assert.True(File.Exists(zipPath));
        Assert.False(Directory.Exists(Path.Combine(root, "images")));
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--out")]
    [InlineData("--receipt")]
    public void OrganizeGeneratesReceiptIdAndWritesOutputFileWithShortSourceOption(string outputOption)
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "photos.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        using (var stream = zip.CreateEntry("Order-Sheet-26-10.jpg").Open()) stream.Write(Photo);
        var receiptPath = Path.Combine(root, "receipts", "import.json");

        var run = Run("organize", "-s", zipPath, "--root", Path.Combine(root, "images"),
            "--tenant", "Customer", outputOption, receiptPath);

        Assert.Equal(0, run.ExitCode);
        Assert.True(File.Exists(receiptPath));
        using var receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
        Assert.StartsWith("Import_", receipt.RootElement.GetProperty("Id").GetString());
        Assert.Equal("OrderSheet26", receipt.RootElement.GetProperty("Exceptions")[0].GetProperty("ItemId").GetString());
        Assert.Equal(receipt.RootElement.GetRawText(), JsonDocument.Parse(run.StandardOutput).RootElement.GetRawText());
    }

    [Fact]
    public void NumericStemAndSingleNumberResolveToDistinctImportDestinations()
    {
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "distinct.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var name in new[] { "Order-Sheet-26-10.jpg", "Order-Sheet-26.jpg" })
                using (var stream = zip.CreateEntry(name).Open()) stream.Write(Photo);
        }

        var run = Run("organize", "--source", zipPath, "--root", Path.Combine(root, "images"),
            "--tenant", "Customer", "--json");

        Assert.Equal(0, run.ExitCode);
        using var receipt = JsonDocument.Parse(run.StandardOutput);
        Assert.Equal(2, receipt.RootElement.GetProperty("Summary").GetProperty("Copied").GetInt32());
        Assert.Equal(2, receipt.RootElement.GetProperty("Exceptions").GetArrayLength());
        Assert.Contains("OrderSheet26_010.jpg", run.StandardOutput);
        Assert.Contains("OrderSheet_026.jpg", run.StandardOutput);
    }

	[Fact]
	public void OrganizeNoOverwriteFlagRejectsChangedDestinationBeforeAddingNewImage()
	{
		Directory.CreateDirectory(root);
		var firstZip = Path.Combine(root, "first.zip");
		var changedZip = Path.Combine(root, "changed.zip");
		using (var zip = ZipFile.Open(firstZip, ZipArchiveMode.Create))
		using (var stream = zip.CreateEntry("Order-Sheet-001.jpg").Open()) stream.WriteByte(1);
		using (var zip = ZipFile.Open(changedZip, ZipArchiveMode.Create))
		{
			using (var stream = zip.CreateEntry("Order-Sheet-001.jpg").Open()) stream.WriteByte(2);
			using (var stream = zip.CreateEntry("Order-Sheet-002.jpg").Open()) stream.WriteByte(3);
		}
		var images = Path.Combine(root, "images");
		var first = Run("organize", "--source", firstZip, "--root", images, "--tenant", "Customer", "--json");
		Assert.Equal(0, first.ExitCode);
		var guarded = Run("organize", "--source", changedZip, "--root", images, "--tenant", "Customer", "--no-overwrite", "--json");
		Assert.Equal(1, guarded.ExitCode);
		using var receipt = JsonDocument.Parse(guarded.StandardOutput);
		Assert.Contains("Destination contains different content", receipt.RootElement.GetProperty("Error").GetString());
		var item = new RaiImage.ItemTreePath(new RaiPath(Path.Combine(images, "Customer")), "OrderSheet");
		Assert.Equal(new[] { "OrderSheet_001.jpg" }, item.SelectFiles().Select(file => file.NameWithExtension).ToArray());
		Assert.Equal(1, File.ReadAllBytes(item.SelectFiles().Single().FullName)[0]);
	}

    [Fact]
    public void ArgumentErrorsDoNotPolluteJsonStdout()
    {
        foreach (var args in new[]
        {
            new[] { "organize", "--source", "source.zip", "--root", root, "--out", "" },
            new[] { "organize", "--source", "source.zip", "--root", root, "--json", "--import-id", "{Placeholder}" },
            new[] { "organize", "--source", "source.zip", "--source-url", "https://example.org/archive.zip", "--root", root, "--json", "--import-id", "Photos001" },
            new[] { "clean", "Photo", "--root", root, "--exif", "*" }
        })
        {
            var run = Run(args);
            Assert.Equal(1, run.ExitCode);
            Assert.Empty(run.StandardOutput);
            Assert.NotEmpty(run.StandardError);
        }
    }

    private static RaiSystemResult Run(params string[] args) => IorgCommand.ForManagedAssembly(new RaiFile(Path.Combine(AppContext.BaseDirectory, "ImgSeeder.dll"))).Run(args);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
