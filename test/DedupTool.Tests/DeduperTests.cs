using System.Text.Json;
using DedupTool;
using Xunit;

namespace DedupTool.Tests;

public class DeduperTests
{
    private static Document Loose(string id, string custodian, string hash) => new()
    {
        DocId = id, Custodian = custodian, DocType = "loose", FileName = id + ".txt", Sha256 = hash, SizeBytes = 10,
    };

    [Fact]
    public void IdenticalLooseDocuments_SecondIsSuppressed()
    {
        var batch = new Batch
        {
            Documents = new() { Loose("A", "c1", "h1"), Loose("B", "c2", "h1") },
        };

        var result = Deduper.Run(batch);

        Assert.Single(result.Rows);
        Assert.Equal("A", result.Rows[0].DocId);
        Assert.Equal(1, result.Suppressed);
    }

    [Fact]
    public void DistinctDocuments_AllKept()
    {
        var batch = new Batch
        {
            Documents = new() { Loose("A", "c1", "h1"), Loose("B", "c1", "h2") },
        };

        var result = Deduper.Run(batch);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void LoadFile_StartsWithFixedHeader()
    {
        var csv = LoadFileWriter.ToCsv(Array.Empty<LoadFileRow>());

        Assert.StartsWith("DocID,FamilyID,ParentID,Custodian,DocType,FileName,SentDate,Hash", csv);
    }

    [Fact]
    public void SampleBatch_KeptEmailsKeepSharedAttachments()
    {
        var batch = LoadBatch(FindUpwards("data", "batch-sample.json"));
        var result = Deduper.Run(batch);

        var forwardPdf = Row(result, "DOC-000005");
        Assert.Equal("DOC-000004", forwardPdf.ParentId);
        Assert.Equal("DOC-000004", forwardPdf.FamilyId);

        var originalPdf = Row(result, "DOC-000003");
        Assert.Equal(originalPdf.Hash, forwardPdf.Hash);

        var mleeSpreadsheet = Row(result, "DOC-000010");
        Assert.Equal("DOC-000009", mleeSpreadsheet.ParentId);
        Assert.Equal("DOC-000009", mleeSpreadsheet.FamilyId);

        var mleePdf = Row(result, "DOC-000011");
        Assert.Equal("DOC-000009", mleePdf.ParentId);
        Assert.Equal("DOC-000009", mleePdf.FamilyId);

        Assert.Contains(result.Rows, r => r.DocId == "DOC-000008");
        Assert.DoesNotContain(result.Rows, r => r.DocId == "DOC-000013");
    }

    [Fact]
    public void NestedAttachment_ParentIsImmediateAndFamilyIsRootEmail()
    {
        const string leafHash = "hash-leaf";
        var batch = new Batch
        {
            Documents = new()
            {
                new Document
                {
                    DocId = "ATT-OTHER", Custodian = "c2", DocType = "attachment", ParentId = "EMAIL-2",
                    FileName = "same.pdf", Sha256 = leafHash, SizeBytes = 5,
                },
                new Document
                {
                    DocId = "EMAIL-2", Custodian = "c2", DocType = "email",
                    FileName = "other.eml", Sha256 = "hash-email-2", SizeBytes = 5,
                    SentDate = "2024-04-01T00:00:00Z", From = "b@example.com",
                    To = new() { "a@example.com" }, Subject = "Other", Body = "Other",
                },
                new Document
                {
                    DocId = "EMAIL-1", Custodian = "c1", DocType = "email",
                    FileName = "root.eml", Sha256 = "hash-email-1", SizeBytes = 5,
                    SentDate = "2024-03-05T14:22:00-05:00", From = "a@example.com",
                    To = new() { "b@example.com" }, Subject = "Root", Body = "Root",
                },
                new Document
                {
                    DocId = "ATT-MID", Custodian = "c1", DocType = "attachment", ParentId = "EMAIL-1",
                    FileName = "nested.eml", Sha256 = "hash-mid", SizeBytes = 5,
                },
                new Document
                {
                    DocId = "ATT-LEAF", Custodian = "c1", DocType = "attachment", ParentId = "ATT-MID",
                    FileName = "same.pdf", Sha256 = leafHash, SizeBytes = 5,
                },
            },
        };

        var result = Deduper.Run(batch);

        Assert.Equal(5, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);

        var mid = Row(result, "ATT-MID");
        Assert.Equal("EMAIL-1", mid.ParentId);
        Assert.Equal("EMAIL-1", mid.FamilyId);

        var leaf = Row(result, "ATT-LEAF");
        Assert.Equal("ATT-MID", leaf.ParentId);
        Assert.Equal("EMAIL-1", leaf.FamilyId);
        Assert.Equal("2024-03-05T14:22:00-05:00", leaf.SentDate);

        var other = Row(result, "ATT-OTHER");
        Assert.Equal("EMAIL-2", other.ParentId);
        Assert.Equal("EMAIL-2", other.FamilyId);
    }

    private static LoadFileRow Row(DedupResult result, string docId) =>
        result.Rows.Single(r => r.DocId == docId);

    private static Batch LoadBatch(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Batch>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"Batch file was empty: {path}");
    }

    private static string FindUpwards(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find " + Path.Combine(relativeParts));
    }
}
