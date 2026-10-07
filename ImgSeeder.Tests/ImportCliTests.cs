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
    public void ArgumentErrorsDoNotPolluteJsonStdout()
    {
        foreach (var args in new[]
        {
            new[] { "organize", "--source", "source.zip", "--root", root, "--json" },
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
