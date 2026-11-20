# Decisions

I started the timed exercise at approximately 15:25 on November 20, 2026, Mexico City time (UTC−06:00). The final implementation commit, `71d4b34`, was recorded at 16:24:30, before the 16:25 deadline. The agreed plan is available in English at [ai/implementation-plan-en.md](ai/implementation-plan-en.md).

## Where the spec was unclear, contradictory, or silent

I contacted the recruiting team to clarify three cases: the same attachment appearing in different email families, the same loose file belonging to different custodians, and copies of the same email collected from different mailboxes. I received no reply before the timed exercise ended, so I prioritized INGEST-4471 and made the following decisions.

- **Attachment deduplication vs. family integrity (§§3.2, 3.4, 3.5, and 3.6).** `vendor-report.pdf` appears on both `DOC-000001` and the forward `DOC-000004`. A CSV row has one `ParentID` and one `FamilyID`. Keeping a single row for that hash would leave one of the two emails without its attachment. I kept both occurrences to preserve each email's context. This pass emits every email and attachment, including repeated hashes within the same family.
- **Global deduplication vs. independent custodian sets (§§3.1, 3.4, and 4).** Keeping one loose-file row per custodian would preserve each custodian's copy. I kept the existing batch-wide behavior for loose files and limited this change to the ticket. `IdenticalLooseDocuments_SecondIsSuppressed` expects one surviving row for two custodians with the same file. Under that behavior, a custodian's set may lack a file if its surviving copy belongs to another custodian.
- **Email identity across mailboxes (§3.3).** The spec requires recognition of copies with different SHA-256 values and leaves the matching fields and normalization rules open. I deferred cross-mailbox suppression. `DOC-000009` remains in the output with both attachments.
- **Choosing which copy to retain (§6.1).** The result must be independent of the input order, and the spec leaves the choice of representative open. The first loose file still wins in this pass. The existing test uses `[A, B]`, so it would also pass with lowest `docId` as the tie-break. Reversing the input would expose the current behavior.

## What I changed and why

- Implemented the ticket on local branch `ingest-4471-keep-family-attachments` from `main` at `c5ebbad`. The timed implementation is in `71d4b34`.
- Kept `data/current-tool-output.csv` as evidence of the original failure. Local `.vscode/` files remained outside the commit.
- Included the agreed plan in English and the original conversation alongside its English translation.

## What was implemented from the plan

- Branch `ingest-4471-keep-family-attachments` from `main` at `c5ebbad`. Local only. No push. `.vscode/launch.json` and `.vscode/tasks.json` stayed untracked. `data/current-tool-output.csv` was left as the pre-fix evidence.
- `Deduper` behavior in section 2 of the plan.
- Both new tests. The three existing tests were not edited.
- `DECISIONS.md`, `ai/implementation-plan-en.md`, `ai/session-original-es.md`, and `ai/session-translation-en.md`.

## What was verified

- `dotnet test` passed with 5 tests, 0 failures, and 0 skipped. The two new tests cover the shared attachment in the sample and the distinction between the immediate parent and the root family for a nested attachment.
- Running the CLI against `data/batch-sample.json` produced `16 documents in, 15 out, 1 suppressed as duplicates`. `DOC-000005` had `ParentID` and `FamilyID` set to `DOC-000004`. `DOC-000003` remained, and `DOC-000010` and `DOC-000011` were linked to `DOC-000009`. Only the loose file `DOC-000013` was omitted.

## What I didn't get to

- Clarify how cross-mailbox deduplication should interact with custodian independence. Evaluate a match using sender, recipients, send instant, subject, and body, with normalization limited to representational differences. Check family equivalence before suppressing a whole family.
- Format dates in UTC with no fractional seconds. For example, `DOC-000001` and its attachments should carry `2024-03-05T19:22:00Z`.
- Once the loose-file deduplication scope is agreed, choose a stable representative such as the lowest `docId`. Run the same batch in both orders and compare the surviving IDs and serialized output.
- Add CSV escaping for commas, quotes, and line breaks. Verify that a CSV reader returns eight columns and the original field values.
- Measure runtime and memory with a representative large batch. The current root lookup can repeat the same parent walks. If those walks are a bottleneck, cache the root for each resolved document.

## What I'd do next

- Implement section 3.3 by suppressing the whole duplicate family. Keep the family with the lower email `docId`. Match on the UTC instant, subject, newline-normalized body, and case-insensitive from. Do not strip `Re:` or `FW:`. Prove it with a duplicate email whose attachment hash appears nowhere else, so the child is omitted because the email was suppressed.
- Format `SentDate` as UTC with no fractional seconds. `DOC-000001` would become `2024-03-05T19:22:00Z`.
- Break loose-file ties by lowest `docId` and add the reversed-order test.
- Quote a field only when it contains a comma, quote, or line break. Leave the header text exact.

## If you reimplemented in another language

- I kept the C# implementation and the existing CLI contract.

## Working with the AI

- I used the assistant in Ask to review the code, sample, and tests. I reviewed the proposed behavior before moving to Plan and approved the scope before Agent made changes.

- The assistant suggested that quoting all CSV fields might break ingestion. I asked what evidence supported that claim. The spec and sample did not support it, so I dropped that assumption.

- The [original conversation](ai/session-original-es.md) is in Spanish. The [English transcript](ai/session-translation-en.md) is a translation of that conversation.

## Post-exercise review

- I verified the timed commit in a temporary copy. All five tests passed again, and the CLI reproduced the same sample result. Additional checks confirmed that repeated attachments within the same family are retained and that a cycle or missing parent stops processing before a CSV is written.

- This review also changed one proposed follow-up test. If two email copies match on message fields but one has a unique attachment, the test should verify that the attachment and its relationship to its email are preserved. This finding was recorded after the timed exercise. The implementation remains at `71d4b34`.
