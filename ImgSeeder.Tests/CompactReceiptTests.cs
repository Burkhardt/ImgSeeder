using System.Text;
using System.Text.Json;
using ImgSeeder;

namespace Iorg.Tests;

public sealed class CompactReceiptTests
{
    [Fact]
    public void Mixed500ImagesUseTwoRangesUnderOneKilobyte()
    {
        var receipt = Receipt();
        Add(receipt, "CustomerOrder", 1, 300);
        Add(receipt, "Invoice", 1, 200);
        receipt.Compact();
        var json = JsonSerializer.Serialize(receipt);
        Assert.True(Encoding.UTF8.GetByteCount(json) < 1000, json);
        using var document = JsonDocument.Parse(json);
        var result = document.RootElement;
        Assert.False(result.TryGetProperty("Files", out _));
        Assert.False(result.TryGetProperty("BaseItemId", out _));
        Assert.False(result.TryGetProperty("Range", out _));
        Assert.Equal(2, result.GetProperty("Ranges").GetArrayLength());
        Assert.Equal(500, receipt.Summary.Copied);
        Assert.All(receipt.Ranges!, r => { Assert.Equal("Structured", r.NamingConvention); Assert.Equal("ItemIdTree8x2", r.PathConvention); });
    }

    [Fact]
    public void HolesFailedItemsAndLooseImagesNeverInventSuccessfulRangeMembers()
    {
        var receipt = Receipt();
        Add(receipt, "Order", 1, 5);
        receipt.Files[2].Status = "Failed";
        receipt.Files[2].Error = "Copy failed.";
        Add(receipt, "Invoice", 1, 1, "png");
        receipt.Compact();
        Assert.Equal(new[] { (1, 2), (4, 5) }, receipt.Ranges!.Select(r => (r.Start, r.End)));
        Assert.Equal(2, receipt.Exceptions!.Count);
        Assert.Equal(5, receipt.Summary.Copied);
        Assert.Equal(1, receipt.Summary.Failed);
        Assert.Contains(receipt.Exceptions, e => e.ItemId == "Invoice" && e.Status == "Copied");
        Assert.Contains(receipt.Exceptions, e => e.Status == "Failed" && e.ImageNumber == 3);
        Assert.Null(receipt.DetailedFiles);
    }

    [Fact]
    public void ExplicitExifKeepsRangeAndDetailedFiles()
    {
        var receipt = Receipt(exif: true);
        Add(receipt, "Order", 1, 4);
        receipt.Compact();
        Assert.NotNull(receipt.Range);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(receipt));
        Assert.Equal(4, document.RootElement.GetProperty("Files").GetArrayLength());
        Assert.Equal(4, document.RootElement.GetProperty("Range").GetProperty("Count").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("Status", out _));
    }

    [Fact]
    public void ExtensionsAreSeparateRangesAndSkippedFilesAreSparseExceptions()
    {
        var receipt = Receipt();
        Add(receipt, "Order", 1, 2, "jpg");
        Add(receipt, "Order", 1, 2, "png");
        receipt.Files.Add(new() { SourceEntry = "info.json", Status = "Skipped", Reason = "Metadata." });
        receipt.Compact();
        Assert.Equal(new[] { "jpg", "png" }, receipt.Ranges!.Select(r => r.Ext));
        Assert.Single(receipt.Exceptions!);
        Assert.Equal(1, receipt.Summary.Skipped);
    }

    private static ImageImportReceipt Receipt(bool exif = false) => new()
    {
        Id = "Import_OrderPhotos", ActivityId = "OrderPhotos", Tenant = "Customer",
        Source = new("Zip", "orders.zip"), IncludeExif = exif
    };
    private static void Add(ImageImportReceipt receipt, string id, int start, int count, string ext = "jpg")
    {
        foreach (var n in Enumerable.Range(start, count)) receipt.Files.Add(new()
        { SourceEntry = $"{id}-{n}.{ext}", ItemId = id, ImageNumber = n, Ext = ext, Status = "Copied" });
    }
}
