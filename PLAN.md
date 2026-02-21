# Terminal.Gui Major Performance Improvement Plan (Post-PR #4735)

## Summary
This plan targets the highest CPU/allocation hotspots outside the `_lastOutputStringBuilder` leak fixed in PR #4735.  
Priority is based on hot-path frequency in the main loop and expected wall-clock/GC impact.

## Priority Workstreams
1. **P0: Sparse redraw fast-path in output flush (biggest likely win).**  
Evidence: `Terminal.Gui/Drivers/Output/OutputBase.cs:59`, `Terminal.Gui/Drivers/Output/OutputBase.cs:70`, `Terminal.Gui/Drivers/Output/OutputBase.cs:72`, `Terminal.Gui/Drivers/Output/OutputBase.cs:89`, `Terminal.Gui/Drivers/Output/OutputBase.cs:142`, `Terminal.Gui/Drivers/Output/OutputBase.cs:338`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:79`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:178`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:334`.  
Implementation: make `OutputBase.Write` skip rows where `DirtyLines[row] == false`, clear `DirtyLines[row]` after flush, avoid unconditional `SetCursorPositionImpl(0,row)`, and gate `Osc8UrlLinker.WrapOsc8` behind a cheap URL sentinel check to avoid `ToString()` on every chunk.  
Expected impact: large CPU drop on mostly-static screens; major allocation reduction in ANSI output.

2. **P0: Remove per-cell expensive grapheme validation/normalization from render hot path.**  
Evidence: `Terminal.Gui/Drawing/Cell.cs:25`, `Terminal.Gui/Drawing/Cell.cs:30`, `Terminal.Gui/Drawing/Cell.cs:42`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:272`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:299`.  
Implementation: add an internal trusted write path for `Cell` grapheme assignment (preserve current validation for external/untrusted entry points), and use it from `OutputBufferImpl`.  
Expected impact: major CPU reduction when drawing large text regions.

3. **P0: Optimize `AddStr`/`FillRect` write loops to reduce allocations and lock churn.**  
Evidence: `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:142`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:144`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:154`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:166`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:363`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:375`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:384`, `Terminal.Gui/Drivers/Output/OutputBufferImpl.cs:385`.  
Implementation: lock once per `AddStr` call, precompute rune string/width once in `FillRect`, avoid per-cell `rune.ToString()`/`GetColumns()` recomputation.  
Expected impact: large reduction in allocation pressure during viewport clears and text rendering.

4. **P1: Remove LINQ/snapshot churn from draw invalidation traversal.**  
Evidence: `Terminal.Gui/App/ApplicationImpl.Screen.cs:62`, `Terminal.Gui/App/ApplicationImpl.Screen.cs:72`, `Terminal.Gui/App/ApplicationImpl.Screen.cs:75`, `Terminal.Gui/App/ApplicationImpl.Screen.cs:85`, `Terminal.Gui/ViewBase/View.Drawing.cs:18`, `Terminal.Gui/ViewBase/View.Drawing.cs:53`, `Terminal.Gui/ViewBase/View.Drawing.cs:692`, `Terminal.Gui/ViewBase/View.NeedsDraw.cs:65`, `Terminal.Gui/ViewBase/View.NeedsDraw.cs:110`, `Terminal.Gui/ViewBase/View.NeedsDraw.cs:136`.  
Implementation: replace repeated `ToArray()/Reverse()/Any()/Where()` with indexed loops over cached arrays/lists for the frame; avoid full subtree `ClearNeedsDraw` when subtree flags are already clear.  
Expected impact: significant CPU/alloc drop in large view hierarchies.

5. **P1: Rework layout dependency ordering from O(VE) per-pass to cached graph + linear topo sort.**  
Evidence: `Terminal.Gui/ViewBase/View.Layout.cs:736`, `Terminal.Gui/ViewBase/View.Layout.cs:739`, `Terminal.Gui/ViewBase/View.Layout.cs:989`, `Terminal.Gui/ViewBase/View.Layout.cs:994`, `Terminal.Gui/ViewBase/View.Layout.cs:1009`, `Terminal.Gui/ViewBase/View.Layout.cs:1017`, `Terminal.Gui/ViewBase/View.Layout.cs:874`.  
Implementation: build dependency graph once when Pos/Dim dependencies change, then reuse; replace repeated `edges.All`/`edges.Where(...).ToArray()` with in-degree queue topo sort.  
Expected impact: strong CPU improvement on complex/dynamic layouts.

6. **P1: Mouse hit-testing and enter/leave diff optimization.**  
Evidence: `Terminal.Gui/App/Mouse/MouseImpl.cs:60`, `Terminal.Gui/App/Mouse/MouseImpl.cs:62`, `Terminal.Gui/App/Mouse/MouseImpl.cs:188`, `Terminal.Gui/App/Mouse/MouseImpl.cs:191`, `Terminal.Gui/App/Mouse/MouseImpl.cs:212`, `Terminal.Gui/ViewBase/View.Layout.cs:1264`, `Terminal.Gui/ViewBase/View.Layout.cs:1275`, `Terminal.Gui/ViewBase/View.Layout.cs:1284`, `Terminal.Gui/ViewBase/View.Layout.cs:1318`, `Terminal.Gui/ViewBase/View.Layout.cs:1329`.  
Implementation: use `HashSet<View>` for enter/leave diff, avoid repeated `List.Contains` chains, and reduce repeated `FrameToScreen()` calls during a single hit-test pass.  
Expected impact: major CPU reduction under high-frequency mouse move/drag events.

7. **P1: Stop forced text reformat on every draw and remove duplicate formatting calls.**  
Evidence: `Terminal.Gui/ViewBase/View.Drawing.cs:439`, `Terminal.Gui/ViewBase/View.Drawing.cs:496`, `Terminal.Gui/ViewBase/View.Drawing.cs:503`, `Terminal.Gui/Text/TextFormatter.cs:611`, `Terminal.Gui/Text/TextFormatter.cs:622`, `Terminal.Gui/Text/TextFormatter.cs:914`.  
Implementation: remove unconditional `TextFormatter.NeedsFormat = true` in draw path, avoid `ReplaceLineEndings()` when format cache is valid, and avoid `GetDrawRegion`+`Draw` double `GetLines()` work per draw.  
Expected impact: large CPU/alloc win for text-heavy controls.

## Public APIs / Interfaces / Types
No public API changes in this plan’s default path.  
All changes are internal to `Terminal.Gui` internals; behavior and existing API contracts stay intact.  
If needed for clean implementation, add only internal helpers (for example trusted cell write helpers or internal span-based write helpers).

## Benchmark and Test Scenarios
1. Add `Tests/Benchmarks/Output/OutputWriteBenchmark.cs` with sparse/dense dirty-cell scenarios at 80x25 and 200x60.  
2. Add `Tests/Benchmarks/Output/OutputBufferWriteBenchmark.cs` for `AddStr` ASCII, combining graphemes, and `FillRect` clear cases.  
3. Add `Tests/Benchmarks/Layout/LayoutDependencyBenchmark.cs` for large dependency graphs (100/500/1000 subviews).  
4. Add `Tests/Benchmarks/Input/MouseHitTestBenchmark.cs` for mouse-move path with deep and wide hierarchies.  
5. Add `Tests/Benchmarks/Text/TextFormatterDrawBenchmark.cs` for repeated redraw without text mutation.  
6. Keep existing behavior tests green (`dotnet test --no-build`) and add targeted regression tests for dirty-line correctness, wide-glyph correctness, and mouse enter/leave semantics.

## Acceptance Criteria
1. Sparse redraw benchmark: at least 50% lower CPU time and near-zero extra allocations versus current baseline.  
2. `AddStr`/`FillRect` benchmarks: at least 40% lower allocations; CPU time at least 25% better.  
3. Layout dependency benchmark: at least 30% CPU reduction at 500+ dependent views.  
4. Mouse hit-test benchmark: at least 40% CPU reduction in move-heavy scenario.  
5. No behavioral regressions in rendering correctness, clipping, wide glyph handling, and input routing.

## Assumptions and Defaults
1. Default optimization target is non-legacy ANSI path first, then legacy path parity.  
2. Correctness is non-negotiable; no visual/output behavior changes are allowed.  
3. Benchmark-driven rollout is required per workstream before merge.  
4. Work is sequenced P0 first (output pipeline and cell/write path), then P1 (draw/layout/mouse/text).
