# Manual test plan — Documents & ingestion

Everything a person should walk through before this feature is considered done. Report failures
by number; fixes are batched after a complete pass rather than one at a time.

**Legend** — ⚠️ needs a real Gemini API key · 🔑 needs a second account · 🧪 deliberately awkward

---

## 0. Setup

- [ ] **0.1** `docker compose up -d` starts Postgres, MinIO **and** Ollama, and the bucket is
      created.
- [ ] **0.2** `docker compose ps` shows `querio-minio-bucket` **and** `querio-ollama-model`
      exited 0 (both run once and stop).
- [ ] **0.2a** `curl http://localhost:11434/api/tags` lists `nomic-embed-text`. The first
      `docker compose up` pulls ~274 MB, so give it a minute before assuming it has failed.
- [x] **0.3** Migrations applied — `QUERIO_CONNECTION_STRING=… dotnet ef database update …`.
- [ ] **0.4** `appsettings.local.json` filled in: connection string and all four
      `ObjectStorage:*`. **No API key is needed** — `Embeddings:Provider` is `Ollama`, which
      embeds locally and is not metered.
- [x] **0.5** API starts with no configuration errors. A missing storage or embedding setting
      should stop it at start-up naming what is missing — try removing one to confirm, then put
      it back.
- [x] **0.6** Web app starts; **Documents** appears in the sidebar and opens.

### Fixtures

The files this plan uses live outside the repository, in `Querio-test-files/` beside it, with
their own README. Several are deliberately broken — that is what they are for.

| File                             | What it is                                       | Expected                          |
| -------------------------------- | ------------------------------------------------ | --------------------------------- |
| `handbook.md`                    | 5 `##` sections, `### Parental` under `## Leave` | Accepted                          |
| `handbook-copy.md`               | Byte-identical to `handbook.md`                  | Recognised as the same document   |
| `notes.txt`                      | 8 paragraphs, no Markdown at all                 | Accepted                          |
| `report.docx`                    | Real `Heading 1`/`Heading 2` styles              | Accepted                          |
| `manual.pdf`                     | 14 pages, a footer per page                      | Accepted                          |
| `scan.pdf`                       | 6 raster-only pages, no text layer               | Fails — no readable text          |
| `locked.pdf`                     | AES-128, password `querio-test`                  | Fails — password-protected        |
| `broken.pdf`                     | First 4,000 bytes of `manual.pdf`                | Fails — could not be read         |
| `sheet.xlsx`                     | A small spreadsheet                              | Refused — unsupported type        |
| `empty.txt`                      | Zero bytes                                       | Refused — empty                   |
| `huge.pdf`                       | 24.8 MB, valid PDF                               | Refused — too large               |
| `fake.txt`                       | A real PNG named `.txt`                          | Refused — the bytes decide        |
| `querio-ingestion-fixture-….txt` | 200-character base name                          | Accepted, and the layout holds    |
| `my “notes” 📄.txt`              | Emoji and typographic quotes                     | Accepted, name displays correctly |

Windows reserves `"` in filenames, so check **4.6** uses typographic quotes rather than straight
ones. `manual.pdf` is the one to use for anything about page numbers, and for section 14's
allowance checks — it is long enough to pause part-way through.

---

## 1. Empty state

- [x] **1.1** A brand-new organization shows the upload panel as the whole empty state — not a
      list with an "Add" button.
- [x] **1.2** The line beneath states the accepted formats and the 20 MB cap.
- [x] **1.3** The storage/document limits sentence appears below it.
- [x] **1.4** No usage strip is shown while there are no documents.

## 2. Uploading — the happy paths

- [x] **2.1** **Choose a file** opens the picker; picking `handbook.md` uploads it.
- [x] **2.2** A toast says it was added and is being processed.
- [x] **2.3** The row appears immediately, without a manual refresh.
- [x] **2.4** Repeat for `notes.txt`, `report.docx`, `manual.pdf` — all four accepted.
- [x] **2.5** 🧪 Select **several files at once** in the picker. Each uploads, one after another,
      and each gets its own toast.

## 3. Drag and drop

- [x] **3.1** Dragging a file over the panel turns its border and background to the accent, and
      the text changes to "Drop to upload".
- [x] **3.2** 🧪 Move the pointer **across inner elements** while dragging — the highlight must
      not flicker.
- [x] **3.3** Dragging out of the panel and releasing elsewhere clears the highlight and uploads
      nothing.
- [x] **3.4** Dropping a file uploads it.
- [x] **3.5** 🧪 Drop **two files at once**. Both upload.
- [ ] **3.6** 🧪 Drop something that is not a file at all (selected text from another page).
      Nothing uploads and nothing breaks.

## 4. Uploads that should be refused

Each must give a readable sentence — never a status code, never a stack trace.

- [ ] **4.1** `sheet.xlsx` — refused as an unsupported type.
- [ ] **4.2** `fake.txt` (a PNG renamed) — refused. **The bytes decide, not the extension.**
- [ ] **4.3** `empty.txt` — refused as empty.
- [ ] **4.4** `huge.pdf` — refused for size.
- [x] **4.5** A file with a very long name (200+ characters) — either accepted and truncated
      visually, or refused with a reason. It must not break the layout.
- [x] **4.6** 🧪 A file named with quotes or emoji, e.g. `my "notes" 📄.txt` — uploads, and the
      name displays correctly in the row.

## 5. Duplicates

- [x] **5.1** Upload `handbook.md` again. It is **not** duplicated.
- [x] **5.2** The toast says it is already here.
- [x] **5.3** The list still shows one copy.
- [x] **5.4** 🧪 Copy `handbook.md` to `handbook-copy.md` and upload it. Still recognised as the
      same document — **the contents decide, not the name.**

## 6. Watching ingestion

- [x] **6.1** A newly uploaded document moves through states without a manual refresh.
- [x] **6.2** States read as **Queued → Reading → Splitting → Embedding → Ready**.
- [x] **6.3** ⚠️ While embedding, the row shows a progress bar and "N of M passages", and N climbs.
- [x] **6.4** On **Ready**, the row shows the passage count.
- [x] **6.5** Once everything is Ready, polling stops — open the browser's network tab and
      confirm requests **stop** rather than continuing every two seconds.
- [x] **6.6** 🧪 Upload a large PDF and leave the tab open for several minutes. It completes; no
      request storm. In the network tab the requests must **follow one another** — never a
      growing column of pending ones.
- [x] **6.7** 🧪 Throttle the network to Slow 3G and upload again. The requests space themselves
      out rather than piling up: still one at a time, just slower.
- [x] **6.8** 🧪 Switch to another tab while a document is ingesting, wait a minute, and come
      back. Nothing was requested while it was hidden, and the state is current on return.

## 7. Documents that cannot be ingested

- [x] **7.1** `scan.pdf` — reaches **Failed**, saying no readable text was found and that a scan
      needs converting first.
- [x] **7.2** `locked.pdf` — reaches **Failed**, saying it is password-protected.
- [x] **7.3** `broken.pdf` — reaches **Failed**, saying it could not be read.
- [x] **7.4** The reason appears in the row itself, not only in a tooltip or the logs.
- [x] **7.5** A failed document can still be deleted.
- [x] **7.6** A failed document does **not** retry forever — check the API log; it should give up
      rather than loop.
- [x] **7.7** Other documents keep ingesting normally while one has failed.

## 8. The passage inspector

- [x] **8.1** Clicking a document's name opens its page.
- [x] **8.2** The header shows status, format, size, passage count and upload date.
- [x] **8.3** Passages are listed in order, numbered from 01.
- [x] **8.4** `handbook.md` passages carry breadcrumbs like `Handbook › Leave › Parental`.
- [ ] **8.5** A **sibling** heading replaces rather than nests — a passage under the second `##`
      must not carry the first one's title.
- [ ] **8.6** `report.docx` passages carry breadcrumbs from its Word heading **styles**.
- [ ] **8.7** `manual.pdf` passages show **page numbers** and no breadcrumbs.
- [ ] **8.8** `notes.txt` passages show neither — and that reads as intentional, not broken.
- [ ] **8.9** Token counts are shown with `≈`.
- [ ] **8.10** With more than 25 passages, **Next** and **Previous** page through them and the
      count reads "26–50 of 148".
- [ ] **8.11** **Previous** is disabled on the first page; **Next** on the last.
- [ ] **8.12** No raw vectors appear anywhere in the interface.
- [ ] **8.14** In psql, `SELECT DISTINCT embedding_model FROM document_chunks;` returns exactly
      one value, and it names the provider you are running — `nomic-embed-text-v1.5@768`. More
      than one value means vectors from two embedding spaces share a column, which retrieval
      must never mix.
- [ ] **8.13** 🧪 Open the inspector for a document that is still **Embedding**. It shows the
      passages that exist so far without breaking.

## 9. Download

- [ ] **9.1** **Download** on the detail page opens the original file.
- [ ] **9.2** The downloaded file is byte-identical to what was uploaded.
- [ ] **9.3** It downloads under its **original name**, not its content hash.
- [ ] **9.4** **Download original** in the row's menu does the same.
- [ ] **9.5** 🧪 Copy the download URL, wait more than 10 minutes, and open it. It should be
      refused — the link is deliberately short-lived.

## 10. Deleting

- [ ] **10.1** **Delete** removes the document from the list.
- [ ] **10.2** A toast confirms it.
- [ ] **10.3** The usage strip drops accordingly.
- [ ] **10.4** The object is gone from storage — check the MinIO console at
      `http://localhost:9001` under `querio-documents`.
- [ ] **10.5** Its passages are gone: reopening the old URL gives a not-found rather than an
      empty inspector.
- [ ] **10.6** 🧪 Delete a document **while it is still embedding**. Nothing crashes; the worker
      does not keep writing passages for it.
- [ ] **10.7** Re-uploading a deleted file works — it is treated as new.

## 11. Permissions 🔑

- [ ] **11.1** As a **Member**, upload a document. It works.
- [ ] **11.2** As that Member, delete **your own** document. It works.
- [ ] **11.3** As that Member, try to delete a document **someone else** uploaded. Refused, with
      a message about needing to be an administrator.
- [ ] **11.4** As an **Admin** or **Owner**, delete someone else's document. It works.

## 12. Isolation 🔑

- [ ] **12.1** Create a second organization. Its Documents page is empty.
- [ ] **12.2** Upload there; the first organization's list is unaffected.
- [ ] **12.3** 🧪 Take a document URL from the first organization and open it while switched to
      the second. It must read as **not found** — not "forbidden", which would confirm it exists.
- [ ] **12.4** Usage in one organization does not count the other's documents.

## 13. Limits

- [ ] **13.1** The usage strip shows storage used and the document count, both against their
      limits.
- [ ] **13.2** The numbers match what has actually been uploaded.
- [ ] **13.3** 🧪 To see the near-limit state without uploading 500 MB, lower
      `MaxStoredBytesPerTenant` in `DocumentLimits.cs`, restart, and confirm the bar turns amber
      past 90%. **Put it back afterwards.**
- [ ] **13.4** 🧪 With the lowered limit, upload past it — refused with a message saying what to
      delete.

## 14. Resilience 🧪

- [ ] **14.1** Upload a large document and **stop the API** (Ctrl-C) while it is embedding.
- [ ] **14.2** Restart the API. Ingestion **resumes on its own** — no operator action, no stuck
      document.
- [ ] **14.3** The finished document has the **right number of passages** — the restart must not
      have duplicated any.
- [ ] **14.4** Stop MinIO, then upload. The failure is reported clearly rather than hanging.
- [ ] **14.5** Restart MinIO; uploading works again without restarting the API.
- [ ] **14.6** ⚠️ Set an invalid `Embeddings:Gemini:ApiKey`, restart, upload. The document ends up
      Failed rather than retrying forever, and the log says what happened.

### The free-tier allowance ⚠️

**Only applies with `Embeddings:Provider` set to `Gemini`.** On the default `Ollama` provider
nothing is metered, so 14.7–14.12 should be skipped — or run deliberately, by switching provider
and supplying a key, if the hosted path is what you want to exercise.

A document of any size will pause part-way through on a free-tier key. That is expected, not a
fault — the point of these checks is that it recovers rather than looping.

- [ ] **14.7** Upload `manual.pdf` and watch it to the end. It may pause, showing **Paused**,
      and then finishes as **Ready** without anyone touching it.
- [ ] **14.8** The passage count **only ever climbs**. If it drops between two glances, the
      resume is throwing away work — report it, that is the bug this section exists for.
- [ ] **14.9** A paused row says which allowance ran out. A throttle should say it resumes
      shortly, not that it is waiting for the _daily_ allowance.
- [ ] **14.10** While paused for a throttle, the row updates itself when it resumes — no manual
      refresh.
- [ ] **14.11** In the inspector, ordinals run `01`, `02`, … with no repeats and no gaps, and the
      final count matches the header — nothing was embedded twice.
- [ ] **14.12** If it did pause, the API log carries `Resuming document … at passage N of M` with
      N where it left off, not 0.

## 15. Interface details

- [ ] **15.1** Dark mode: every state pill, the progress bars and the breadcrumb chips are legible.
- [ ] **15.2** At a phone width the rows do not overflow and long file names truncate.
- [ ] **15.3** Keyboard only: the upload button, each row's menu, and pagination are all reachable
      and operable.
- [ ] **15.4** The row menu opens **anchored to its button**, not at the corner of the page.
- [ ] **15.5** Loading skeletons appear on first load rather than an empty flash.
- [ ] **15.6** A slow network (throttle in devtools) does not produce duplicate uploads from
      double-clicking.
- [ ] **15.7** Browser back from the detail page returns to the list in its previous state.

## 16. After deployment (phase 8)

Repeat against production once merged:

- [ ] **16.1** Upload each format.
- [ ] **16.2** Ingestion completes — this is the **first real use of the Gemini key**.
- [ ] **16.3** Breadcrumbs and page numbers are correct.
- [ ] **16.4** Download works against Cloudflare R2, not just MinIO.
- [ ] **16.5** Delete removes the object from the R2 bucket.
- [ ] **16.6** 🧪 Leave it idle for 20 minutes, then load the page. The first request is slow —
      Render and Neon both wake — but it works. Confirm nothing times out.

---

## Reporting

Note the number, what you did, what happened, what you expected. Screenshots help for anything
visual. Keep going to the end of a section rather than stopping at the first failure — unless
something blocks you from continuing, in which case say so and it gets fixed immediately.
