namespace DedupTool;

/// <summary>
/// Writes one row per email and per attachment. Loose files with the same
/// SHA-256 collapse to the first copy in input order. Attachment hashes are
/// not used to drop a later family member.
/// </summary>
public static class Deduper
{
    public static DedupResult Run(Batch batch)
    {
        var byId = new Dictionary<string, Document>();
        foreach (var d in batch.Documents)
        {
            byId[d.DocId] = d;
        }

        var seenLoose = new HashSet<string>();
        var rows = new List<LoadFileRow>();
        var suppressed = 0;

        foreach (var d in batch.Documents)
        {
            if (d.DocType == "loose")
            {
                if (!seenLoose.Add(d.Sha256))
                {
                    suppressed++;
                    continue;
                }

                rows.Add(ToRow(d, d.DocId, ""));
                continue;
            }

            var root = FindRoot(d, byId);
            rows.Add(ToRow(d, root.DocId, root.SentDate ?? ""));
        }

        return new DedupResult(rows, suppressed);
    }

    private static LoadFileRow ToRow(Document d, string familyId, string sentDate) => new()
    {
        DocId = d.DocId,
        FamilyId = familyId,
        ParentId = d.ParentId ?? "",
        Custodian = d.Custodian,
        DocType = d.DocType,
        FileName = d.FileName,
        SentDate = sentDate,
        Hash = d.Sha256,
    };

    private static Document FindRoot(Document start, Dictionary<string, Document> byId)
    {
        var seen = new HashSet<string>();
        var current = start;
        while (!string.IsNullOrEmpty(current.ParentId))
        {
            if (!seen.Add(current.DocId))
            {
                throw new InvalidOperationException($"Parent chain contains a cycle at '{current.DocId}'.");
            }

            if (!byId.TryGetValue(current.ParentId, out var parent))
            {
                throw new InvalidOperationException($"Parent '{current.ParentId}' of '{current.DocId}' is not in the batch.");
            }

            current = parent;
        }

        if (!seen.Add(current.DocId))
        {
            throw new InvalidOperationException($"Parent chain contains a cycle at '{current.DocId}'.");
        }

        return current;
    }
}
