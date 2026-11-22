# Parallel Search Engine with LLM Query Expansion — Design

Date: 2026-11-22
Course: «Паралельні обчислення. Курсова робота» (KPI, IPSA), 2026–2027
Source spec: `Курсова_ПО_2026-2027.pdf` (syllabus + Appendix A/B)

## 1. Intent

Pass the coursework with a high grade. Grading = up to 60 points from an
automated check of a fixed checklist (Compose, index, threads, keyword search,
own parallel data structure, LLM query expansion, WebSocket, CLI, Web UI,
README/clean code, scalability study) + up to 40 points at the defense (demo,
UML, commit history, theory questions from Appendix B).

So the system must (a) satisfy the fixed API contract exactly, (b) start with
one `docker compose up`, and (c) be built around **hand-written parallel code
the student can explain**: own thread pool, own thread-safe inverted index,
parallel request processing. Measurable speedup and a clear bottleneck story
matter for the report.

## 2. Goals / non-goals / anti-goals

**Goals**
- C# backend (.NET 11) exposing the exact REST + WebSocket contract.
- Parallel inverted-index build on an own thread pool, thread count variable
  per request; build time drops as threads increase.
- Own striped-lock hash map for the index, plus a single-global-lock mode for
  benchmark comparison.
- Search pipeline: LLM query expansion (Ollama, Gemma 4) → BM25 keyword search.
- Two clients: .NET console CLI (`./search "query"`) and a Spotlight-style
  macOS-looking Web UI with live pipeline stages over WebSocket.
- Benchmarks + charts: build time vs threads (both lock modes), speedup,
  throughput vs concurrent clients.
- UML (use case, class, sequence per use case, deployment), README,
  conventional commits on GitHub.

**Non-goals**
- Index persistence to disk (index lives in memory; rebuilt via API).
- Auth, phrase/boolean queries, stemming, non-English text, vector/semantic
  search, distributed/multi-node index, search modes other than `keyword`.
- The PDF report itself (student writes it; we provide charts, diagrams,
  tables and notes to draw from).

**Anti-goals (fail even if it "works")**
- Using `System.Threading.ThreadPool`, `Parallel.*`, `Task.Run` fan-out,
  `ConcurrentDictionary`, `BlockingCollection` or `Channel<T>` *inside the
  index or the thread pool* — the spec demands own implementations and the
  code is checked. (Framework concurrency is fine elsewhere, e.g. Kestrel.)
- Any race condition in the index or pool.
- `docker compose up` needing manual steps to reach a healthy server.
- Few/huge commits or non-conventional commit messages.

## 3. Constraints

- Language: C# (spec allows C++/Java/C#). SDK installed: .NET 11.0.100;
  images `mcr.microsoft.com/dotnet/sdk:11.0` / `aspnet:11.0`.
- Host: MacBook, Apple Silicon (arm64), 10 cores, 16 GB RAM, OrbStack for
  Docker. Containers get no Apple GPU → LLM runs on CPU in Docker.
- Dataset: English, ≥10,000 documents. The spec says both "≥2 GB" and
  "≥1 GB"; we target **~2 GB** to satisfy the stricter one (size is a script
  parameter).
- Milestone: first attestation (weeks 7–8) needs Compose + `GET /api/health`
  200 + UML use case/class/sequence.

## 4. Approaches considered (decided with the student)

**HTTP server**
1. ASP.NET Core only — least code, but parallel request handling is all
   framework.
2. Hand-rolled HTTP/WebSocket on `TcpListener` — best theory story, ~2× server
   code, higher risk of failing the automated check.
3. **Hybrid (chosen):** Kestrel accepts connections and does async I/O; the
   CPU work of each search is dispatched to the *own* thread pool. The same
   pool implementation also builds the index. Gives a direct answer to
   "how do you process requests in parallel?" and an interesting throughput
   curve.

**Index concurrency**
1. **Striped locks + global-lock baseline (chosen):** own hash map split into
   N segments, each with its own lock; switchable to one global lock for the
   benchmark/bottleneck analysis.
2. Lock-free CAS hash map — stronger Q12/13 answers, much higher race risk.
3. Both — best report, ~1.5× index work.

**Dataset:** **English Wikipedia slice (chosen)** over teacher's dataset
(not available), IMDB+filler (fake-looking results), fully synthetic (poor
demo).

**CLI:** **.NET console app (chosen)** over bash+curl+jq / Python.
**Web UI:** **Plain HTML/JS served by the server (chosen)** over React+Vite
container / Blazor WASM. Visual style: **Spotlight (chosen)**.
**LLM:** **Gemma 4, smallest tag `gemma4:e2b` (chosen)**, configurable.

## 5. Architecture

```
docker compose up
├── ollama          image ollama/ollama, volume ollama-models, port 11434
├── ollama-pull     one-shot: waits for ollama, runs `ollama pull $OLLAMA_MODEL`
└── search-server   ASP.NET Core (Kestrel), port 8080
     ├── REST  GET /api/health · POST /api/index · POST /api/search · GET /api/stats
     ├── extra GET /api/doc/{doc_id}  (full text for UI preview)
     ├── WS    /ws/search
     ├── wwwroot/ → Spotlight Web UI
     └── SearchEngine (class library, all own code)
          ├── Concurrency/WorkerPool      own thread pool
          ├── Concurrency/BlockingQueue   own bounded queue (Monitor.Wait/Pulse)
          ├── Index/ConcurrentTermIndex   striped-lock hash map (or global lock)
          ├── Index/IndexBuilder          parallel build over files
          ├── Text/Tokenizer              lowercase, split non-letters, stopwords
          ├── Search/Bm25Searcher         ranking + snippets
          └── Llm/OllamaQueryExpander     POST /api/generate
./search "query"  → SearchCli (.NET console) → REST/WS
```

`search-server` `depends_on: ollama` (started), **not** on the pull finishing,
so `/api/health` returns 200 immediately; it reports `ollama: false` until the
model is available. `./data` is mounted read-only at `/data`.

## 6. Components

### 6.1 Own thread pool (`WorkerPool`)
- Fixed number of dedicated `Thread`s (named, background), created in ctor.
- Work items in own `BlockingQueue<T>`: array ring buffer + `Monitor`
  (`lock`, `Wait`, `PulseAll`), bounded (back-pressure on submit), supports
  `CompleteAdding`.
- API: `Submit(Action)`, `Submit<T>(Func<T>) → Task<T>` (via
  `TaskCompletionSource` so Kestrel handlers can `await` without blocking),
  `WaitAll()` / countdown for batch jobs, `Dispose()` = drain + join.
- Exceptions in work items are captured into the returned task / batch error,
  never kill a worker.
- Two instances at runtime: a long-lived **search pool**
  (`SEARCH_THREADS`, default = CPU count) and a short-lived **build pool**
  created per `POST /api/index` with `threads` workers.

### 6.2 Inverted index (`ConcurrentTermIndex`)
- Own open-hashing table: buckets array of entry chains, keyed by term string
  (own hash = `string.GetHashCode` with a fixed-seed alternative is fine;
  builder's call), value = posting list.
- **Striped mode:** `S` segments (default 256, power of two), segment chosen by
  hash bits; each segment = own small hash table + own lock object; each
  segment resizes independently under its own lock.
- **Global mode:** same code, `S = 1` (one lock) — the benchmark baseline.
- Posting list: own growable struct array of `(int docId, int tf)`, appended
  under the segment lock. Each doc's terms are pre-aggregated locally
  (per-doc term→tf map, thread-local) before inserting, so one insert per
  (term, doc).
- Document table: `DocInfo[]` (path, title, length in tokens) indexed by
  docId; docIds pre-assigned from the sorted file list, so writes to it never
  contend.
- After build: index is frozen (read-only); searches read it without locks.
  Posting lists sorted by docId at freeze time (parallel, on the build pool).

### 6.3 Index build (`POST /api/index`)
1. Enumerate `*.txt` under `DATA_DIR` (main request thread), sort, assign ids.
2. Split into batches (default 64 files) and submit each batch to a new
   `WorkerPool(threads)`.
3. Each task: read file, tokenize, local tf map, insert into shared index,
   record doc length.
4. Wait for all, freeze, compute avg doc length, swap the new index into the
   engine with an atomic reference swap (`Volatile.Write`/`Interlocked`).
- A second `POST /api/index` while one is running → `409 {"error": ...}`.
- Searches keep using the previous index during a rebuild.
- Optional extra request fields (defaults keep the fixed contract):
  `lock_mode: "striped"|"global"` (default striped), `segments` (default 256).
- `threads` default = CPU count; validate 1..256.

### 6.4 Search
- Tokenize query (same tokenizer). If `expand` (default true) → LLM expansion
  (async HTTP on the Kestrel side, *not* on a pool thread) → expanded terms
  tokenized, de-duplicated, minus original terms.
- Ranking work submitted to the search pool: BM25 (k1 = 1.2, b = 0.75),
  original terms weight 1.0, expanded terms weight 0.5; score accumulator per
  query; top-K (default 10, optional `top_k`) via a small heap.
- `score` normalized to 0..1 by dividing by the top score.
- `snippet`: read the doc file, find the first occurrence of any matched term,
  ±120 chars window, whitespace-collapsed. `doc_id` = file path relative to
  `DATA_DIR` without extension; response rows also carry `title` (extra field).
- No index yet → `409 {"error":"index not built"}`.
- Unknown `mode` → `400`.

### 6.5 LLM query expansion (`OllamaQueryExpander`)
- `POST {OLLAMA_URL}/api/generate`, `model = OLLAMA_MODEL` (default
  `gemma4:e2b`), `stream: false`, `format: "json"`, `options.temperature: 0`.
- Prompt asks for a JSON object `{"terms": [...]}` with up to 8 single-word
  English keywords/synonyms related to the query.
- Timeout `OLLAMA_TIMEOUT_MS` (default 20000). On error/timeout/bad JSON →
  expansion empty, search continues with original terms; the response/WS
  stage reports `expanded_terms: []` and an `error` note.
- No caching (keeps benchmarks honest).
- Health check: `GET {OLLAMA_URL}/api/tags` and model present → `ollama: true`.

### 6.6 REST contract (fixed by spec; extra fields allowed)
| Endpoint | Request | Response |
|---|---|---|
| `GET /api/health` | — | `{"status":"ok","ollama":true\|false}` |
| `POST /api/index` | `{"threads":4}` | `{"status":"ok","documents_indexed":N,"time_ms":T}` |
| `POST /api/search` | `{"query":"text","mode":"keyword"}` (+ optional `expand`, `top_k`) | `{"results":[{"doc_id","score","snippet",+title}],"time_ms":T,+expanded_terms}` |
| `GET /api/stats` | — | `{"documents":N,"index_build_time_ms":T,"threads_used":K,+terms,+postings,+lock_mode}` |
| `GET /api/doc/{doc_id}` (extra) | — | `{"doc_id","title","text"}` |

JSON uses snake_case.

### 6.7 WebSocket `/ws/search`
- Client sends `{"query":"..."}` (optional `top_k`); may send several queries
  sequentially on one connection.
- Server sends one JSON message per stage:
  `{"stage":"query_expansion","data":{"original":"...","expanded_terms":[...]}}`
  → `{"stage":"keyword_search","data":{"results":[...],"time_ms":12}}`
  → `{"stage":"done"}`. Errors: `{"stage":"error","data":{"message":"..."}}`.

### 6.8 CLI (`SearchCli`, .NET console)
- `./search "query"` — default: WebSocket, prints each stage as it arrives
  (expanded terms, then ranked results with score, title, snippet, colored).
- Flags: `--rest`, `--no-expand`, `--top N`, `--server URL`
  (default `http://localhost:8080`, env `SEARCH_SERVER`), `--index [threads]`,
  `--stats`, `--health`.
- `./search` at repo root: bash wrapper that runs the published binary,
  publishing it on first run (`dotnet publish -c Release`).

### 6.9 Web UI (Spotlight style)
- Served from `wwwroot` at `/`. Plain HTML + CSS + vanilla JS, no build step.
- Look: macOS Spotlight. Centered floating rounded panel with frosted glass
  (`backdrop-filter: blur saturate`), system font stack
  (`-apple-system, BlinkMacSystemFont, "SF Pro"…`), magnifier icon, large
  input, soft shadow, subtle wallpaper-like gradient background, light/dark
  via `prefers-color-scheme`.
- Pipeline stages shown as small pills under the bar that animate
  pending → active → done: "Expanding query" (shows expanded-term chips) →
  "Searching" (shows time) → "Done".
- Results list like Spotlight rows: document icon, title, snippet with
  matched terms highlighted, score; keyboard ↑/↓ to move, Enter/click opens a
  right-hand preview pane with full text (`GET /api/doc`). `⌘K`/`/` focuses
  the bar; `Esc` clears.
- Small footer/status chip: doc count, Ollama status, and a "Rebuild index"
  popover with a threads slider (calls `POST /api/index`, shows time).
- Search runs on Enter (not per keystroke, LLM is slow).

### 6.10 Dataset
- `scripts/fetch_dataset.py` (Python, venv in `scripts/.venv`,
  requirements: `pyarrow`, `requests`): downloads parquet shards of Hugging
  Face `wikimedia/wikipedia` `20231101.en`, writes one UTF-8 `.txt` per
  article (first line = title, then text) into `data/wiki/NNNN/ID.txt`
  (1,000 files per subfolder), skips articles < 500 chars, stops at
  `--size-gb` (default 2.0).
- `data/` is git-ignored except `data/sample/` — a few hundred small
  committed documents so `docker compose up` + `POST /api/index` work out of
  the box. `DATA_DIR` env (default `/data`) — indexes everything under it.

## 7. Benchmarks & report material
- `bench/SearchBench` (.NET console) against a running server:
  - **Build sweep:** threads ∈ {1,2,4,6,8,10,12,16}, both lock modes,
    3 runs each, median → `bench/results/build.csv`.
  - **Throughput:** C ∈ {1,2,4,8,16,32,64} concurrent clients, each looping
    `POST /api/search` with `expand:false` over a fixed query list for 20 s →
    req/s and p50/p95 latency → `bench/results/throughput.csv`.
    Optional run with expansion on to show the LLM bottleneck.
- `scripts/plot.py` (matplotlib) → PNGs in `docs/report/`: build time vs
  threads (striped vs global), speedup S(p)=T1/Tp with ideal line, throughput
  vs clients, latency vs clients.
- `docs/report/search-quality.md`: table of ~10 test queries
  (query, expected, actual top results).
- Bottleneck notes: disk I/O vs CPU, lock contention (global vs striped),
  Amdahl (sequential enumeration/freeze), LLM latency in the pipeline.

## 8. Quality
- xUnit tests: queue/pool (all items run, back-pressure, shutdown, exception
  capture), index (parallel build == sequential build on same corpus, stress
  test with many threads on hot terms, both modes), tokenizer, BM25 ranking
  on a tiny corpus, API contract tests via `WebApplicationFactory` with a
  fake expander.
- `.editorconfig` + `dotnet format --verify-no-changes`.
- GitHub Actions: build, test, format check on push.

## 9. Repo, docs, git
- Public GitHub repo `course_work_parallel_computing`, local path
  `~/Workspace/pccw`. Conventional commits, small atomic commits from the
  first one.
- Layout: `src/SearchEngine`, `src/SearchServer`, `src/SearchCli`,
  `tests/SearchEngine.Tests`, `tests/SearchServer.Tests`, `bench/SearchBench`,
  `scripts/`, `data/sample/`, `docs/uml/` (PlantUML `.puml` + rendered PNG),
  `docs/report/`, `docker-compose.yml`, `docker-compose.dev.yml`,
  `README.md`.
- `docker-compose.dev.yml`: points `OLLAMA_URL` at host-native Ollama
  (`http://host.docker.internal:11434`) for fast GPU expansion during
  development.
- UML: use case; class; sequence for each use case (build index, REST search,
  WS search, health, stats, CLI search, Web UI search); deployment (Compose
  topology).

## 10. Order of work
1. **M1 (first attestation):** repo, solution skeleton, Dockerfile, Compose
   (server + ollama + pull), `/api/health`, UML use case/class/sequence.
2. **M2:** engine — queue, pool, index, tokenizer, builder, `/api/index`,
   `/api/stats`, tests.
3. **M3:** search — BM25, snippets, Ollama expander, `/api/search`,
   `/api/doc`, `/ws/search`, tests.
4. **M4 (in parallel):** CLI ‖ Web UI — both depend only on the M3 API.
5. **M5:** dataset script, benchmarks, charts, README, remaining UML, report
   material.

## 11. Left to the builder
Exact class/file names inside projects, hash function details, batch size
tuning, stopword list, exact prompt wording, CSS details within the
Spotlight look, chart styling, test granularity.
