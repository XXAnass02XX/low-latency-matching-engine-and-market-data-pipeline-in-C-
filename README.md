Good choice. A matching engine is the best C# project for this target because it forces everything the roadmap covered: memory layout, allocation-free code, concurrency, binary protocols and honest latency measurement. Staying in C# is fine for learning and for trading-system roles at banks, brokers and crypto venues. Pure HFT shops mostly hire C++, but the concepts transfer, and later porting the hot core to C++ would make a strong CV line.



\# Project: a low-latency matching engine and market-data pipeline in C#



\## Architecture



```

TCP gateway → \[ring] → Sequencer/Journal → \[ring] → Matching engine (1 thread per symbol shard)

&#x20;                                                       ↓ \[ring]

&#x20;                                   Publisher: UDP multicast (incremental + snapshot) + execution reports

Subscriber/client: gap detection, snapshot recovery, book rebuild

```



The matcher is single-threaded and owns its data, so it needs no locks. Parallelism comes from sharding by symbol.



\## Milestones (each with an acceptance test)



\*\*M0: Naive reference engine first.\*\* Build it with `SortedDictionary` plus queues, correct but slow. It is your oracle for every later test.



\*\*M1: The real order book.\*\*

\- Price as `long` ticks and quantity as `long`, with no `decimal` or `double`.

\- Orders live in a pre-allocated array pool and are linked by `int` indices (an intrusive doubly-linked list per price level), with no object references.

\- Price levels are array-indexed by tick offset, with a bitmap plus `BitOperations.TrailingZeroCount/LeadingZeroCount` to find the next non-empty level in O(1).

\- The order-ID lookup is a custom open-addressing hash map, pre-sized.

\- \*Test:\* random order flow gives identical output versus M0.



\*\*M2: Matching rules.\*\* Price-time priority; limit, market, IOC, FOK and post-only orders; cancel; replace (priority lost unless quantity is reduced); self-trade prevention. It emits Ack, Reject, Fill and Cancel events. \*Test:\* property-based tests (FsCheck) check these invariants:

\- the book never crosses;

\- quantity is conserved;

\- FIFO holds within a level.



\*\*M3: Determinism and journaling.\*\*

\- Every input gets a sequence number and is appended to a memory-mapped binary journal.

\- Replaying the journal must produce a byte-identical output stream (compare hashes).

\- Add snapshot + tail-replay recovery.



\*\*M4: Binary protocol.\*\* Design a fixed-layout ITCH/SBE-like format with explicit `\[StructLayout]` structs, parsed zero-copy from `Span<byte>` using `MemoryMarshal`/`Unsafe`. \*Test:\* 0 bytes allocated per message, enforced in the test via `GC.GetAllocatedBytesForCurrentThread()`.



\*\*M5: Concurrency pipeline.\*\*

\- Write your own SPSC ring buffer with cache-line-padded cursors.

\- Use dedicated threads, CPU-pinned on Linux via P/Invoke `sched\_setaffinity`.

\- Benchmark against `Channel<T>` and show the difference.



\*\*M6: Network layer.\*\*

\- A TCP gateway (`TCP\_NODELAY`, `System.IO.Pipelines` or raw sockets).

\- A UDP multicast publisher with sequence numbers, plus a client that detects gaps and recovers from a snapshot.



\*\*M7: Pre-trade risk.\*\* O(1) checks in the critical path: fat-finger price bands, max order size, position/notional limits, rate limiting, and a kill switch.



\*\*M8: Measurement (what makes it credible).\*\*

\- An \*\*open-loop\*\* load generator, to avoid coordinated omission.

\- HdrHistogram with p50/p99/p99.9/max.

\- GC pause tracking and warmup.

\- Comparisons of Workstation vs Server GC, Tiered PGO on/off, and ReadyToRun vs NativeAOT.

\- A profile from `perf` or PerfView.



\## Stretch

\- A FIX gateway (your own minimal parser, or QuickFIX/n).

\- A market-making bot trading against your engine.

\- A backtester replaying your journal.

\- Hot standby by tailing the journal.

\- Opening/closing auctions, iceberg orders, pro-rata matching.



\## C#-specific rules

\- \*\*Zero allocation on the hot path.\*\* That means no LINQ, `async`, strings, exceptions, boxing or closures there. Pre-allocate everything at startup.

\- Use `readonly struct` types, and pass handlers as `where T : struct, IHandler` generics so the JIT specializes them, instead of interfaces or delegates.

\- Use `\[SkipLocalsInit]` and `AggressiveInlining` only where a benchmark shows a win.

\- Use `GC.TryStartNoGCRegion` or a tuned GC config for a session, and verify with counters, not belief.

\- Develop on WSL2 if you like, but take the latency numbers on native Linux. WSL adds timing noise.



\## Realistic targets (calibrate on your machine, treat as goals not promises)

\- Book core: tens to low hundreds of nanoseconds per add or cancel.

\- In-process pipeline with ring buffers: low single-digit microseconds at p99.

\- Over TCP loopback: tens of microseconds. Report whatever you really measure.



\## Repo layout

`Engine.Core` (no dependencies) · `Engine.Protocol` · `Engine.Journal` · `Engine.Net` · `Engine.Host` · `LoadGen` · `Tests` (property + replay) · `Benchmarks`.



\## Deliverables

A README with the architecture, latency histograms, a proof of 0 allocations, a proof of replay determinism, design decisions, and known limitations. Add one write-up on a hard decision, such as array-indexed levels vs a sorted structure. CI should run the tests.



\## Timeline and reading

About 10-14 weeks part-time. Read the order types and market structure chapters of Harris's \*Trading and Exchanges\*, the LMAX Disruptor paper, the memory chapters of Kokosa, and the classic "How to Build a Fast Limit Order Book" blog post (from memory, so search for it).



Want me to scaffold M0 and M1 (the domain structs, the reference engine and the property-test harness)?

