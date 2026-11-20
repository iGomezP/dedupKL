# Decisions

The agreed plan is translated in [ai/implementation-plan-en.md](ai/implementation-plan-en.md). That file is a translation of the plan in the conversation. It is not a second plan, and it was not rewritten after the code landed. This file records what was implemented and what was verified.

## Where the spec was unclear, contradictory, or silent

- Sections 3.2, 3.4, and 3.6 say two attachments with the same sha256 are duplicates and that suppressed duplicates are omitted. Section 3.5 says a reviewer who opens an email must see every attachment that was on it. `vendor-report.pdf` is on `DOC-000001` and on the forward `DOC-000004`. One row has one `ParentID` and one `FamilyID`, so it cannot hang off both emails. Decision: if an email is emitted, every attachment in its parent chain is emitted, even when that hash is also emitted on another email. In this pass the hash does not suppress emails or attachments at all, including two identical attachments under the same email. The interviewer was asked and had not answered, so the ticket was done first.
- Section 4 says each custodian's production set must stand alone. Sections 3.1 and 3.4 dedupe loose files across the whole batch. Decision: loose-file dedup stays global, and the first copy in the input array wins. `IdenticalLooseDocuments_SecondIsSuppressed` still expects one row for custodians `c1` and `c2`. A severed custodian will not have a loose file whose surviving copy belongs to someone else. That test matches sections 3.1 and 3.4 and conflicts with a strict reading of section 4. It was kept, and the conflict is recorded here. It was not kept in order to hide a broken assertion: it does not require the attachment bug.
- Section 6.1 says the same batch must produce the same load file regardless of input order. The loose-file winner is still whoever appears first in the array. Not changed in this pass. The existing test still passes because `A` is both first and the lower `docId`. The code, not that assertion, is what conflicts with section 6.1.
- Section 3.3 says the same email from more than one mailbox is a duplicate even when the sha256 differs. The match key is not defined. Not implemented. `DOC-000009` remains, now with `DOC-000010` and `DOC-000011`.
- Section 5 requires `SentDate` in UTC as `YYYY-MM-DDTHH:MM:SSZ`. This pass copies the root document's original string onto every member of the chain. It does not convert the offset. Nested attachments no longer go out with an empty date.
- CSV quoting is unspecified. The writer is unchanged. This sample has no comma, quote, or line break inside a field. An earlier guess that quoting might break ingest is not supported by the spec or the sample.

## What I changed and why

- Emails that are emitted keep every attachment, so a shared PDF no longer strips a later family. `DOC-000005` is back on `DOC-000004` (`ParentID` and `FamilyID` are `DOC-000004`) while `DOC-000003` stays. This is INGEST-4471.
- `ParentID` is the immediate parent. `FamilyID` is the document with no parent at the top of the chain. A nested PDF keeps its middle parent and takes the root email as `FamilyID`.
- Attachment rows carry the root's original `SentDate` string.
- Loose files still collapse on sha256, first in input order. That hash set cannot suppress an email or an attachment.
- A parent chain with a cycle, or a parent id that is not in the batch, throws `InvalidOperationException`.

## What was implemented from the plan

- Branch `ingest-4471-keep-family-attachments` from `main` at `c5ebbad`. Local only. No push. `.vscode/launch.json` and `.vscode/tasks.json` stayed untracked. `data/current-tool-output.csv` was left as the pre-fix evidence.
- `Deduper` behavior in section 2 of the plan.
- Both new tests. The three existing tests were not edited.
- `DECISIONS.md`, `ai/implementation-plan-en.md`, `ai/session-original-es.md`, and `ai/session-translation-en.md`.

## What was verified

- `dotnet test` from the repo root: passed, 5 tests, 0 failed, 0 skipped. That includes the three existing tests, `SampleBatch_KeptEmailsKeepSharedAttachments`, and `NestedAttachment_ParentIsImmediateAndFamilyIsRootEmail`.
- `dotnet run --project src/DedupTool -- data/batch-sample.json` to a temp CSV: `batch-sample: 16 documents in, 15 out, 1 suppressed as duplicates.` `DOC-000005` had `ParentID` and `FamilyID` `DOC-000004`. `DOC-000003` was still present. `DOC-000010` and `DOC-000011` were tied to `DOC-000009`. `DOC-000008` was present. `DOC-000013` was absent. The temp CSV was deleted.

## What I didn't get to

- Cross-mailbox email dedup (section 3.3).
- UTC formatting of `SentDate`.
- An order-independent tie-break (section 6.1) and stable CSV row order.
- CSV quoting.
- The one-line summary text. It already reports documents in, out, and suppressed.
- A volume test for section 6.2.

## What I'd do next

- Implement section 3.3 by suppressing the whole duplicate family. Keep the family with the lower email `docId`. Match on the UTC instant, subject, newline-normalized body, and case-insensitive from. Do not strip `Re:` or `FW:`. Prove it with a duplicate email whose attachment hash appears nowhere else, so the child is omitted because the email was suppressed.
- Format `SentDate` as UTC with no fractional seconds. `DOC-000001` would become `2024-03-05T19:22:00Z`.
- Break loose-file ties by lowest `docId` and add the reversed-order test.
- Quote a field only when it contains a comma, quote, or line break. Leave the header text exact.

## If you reimplemented in another language

- No.

## Working with the AI

- Diagnosis, the spec conflict, and this scope were worked out with the assistant before editing. The original conversation is [ai/session-original-es.md](ai/session-original-es.md), in Spanish. [ai/session-translation-en.md](ai/session-translation-en.md) is a faithful English translation of that file, not a separate session.
- The assistant first guessed that CSV quoting might break ingest. Nothing in the spec or the sample supports that, and the guess was dropped.
- A failing behavior was not kept just to leave a test green. The loose-file test matches sections 3.1 and 3.4 and conflicts with a strict reading of section 4. That choice is recorded above.
