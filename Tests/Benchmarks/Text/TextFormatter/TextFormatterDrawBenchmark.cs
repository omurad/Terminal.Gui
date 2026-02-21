using BenchmarkDotNet.Attributes;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using TuiAttribute = Terminal.Gui.Drawing.Attribute;
using TuiColor = Terminal.Gui.Drawing.Color;
using TuiTextFormatter = Terminal.Gui.Text.TextFormatter;

namespace Terminal.Gui.Benchmarks.Text;

[MemoryDiagnoser]
[BenchmarkCategory (nameof (TuiTextFormatter))]
public class TextFormatterDrawBenchmark
{
    private readonly Rectangle _drawRect = new (0, 0, 90, 8);
    private readonly TuiAttribute _normalAttribute = new (TuiColor.White, TuiColor.Black);
    private readonly TuiAttribute _hotAttribute = new (TuiColor.BrightYellow, TuiColor.Black);
    private TuiTextFormatter _formatter = null!;
    private IDriver _driver = null!;

    [GlobalSetup]
    public void Setup ()
    {
        Application.Init (driverName: "ANSI");
        _driver = Application.Driver!;

        _formatter = new TuiTextFormatter ()
        {
            Text =
                "Terminal.Gui is a cross-platform toolkit for building console applications. "
                + "This benchmark measures repeated draw calls without text mutation.",
            Alignment = Alignment.Start,
            VerticalAlignment = Alignment.Start,
            WordWrap = true,
            MultiLine = true,
            ConstrainToSize = _drawRect.Size
        };

        _formatter.FormatAndGetSize (_drawRect.Size);
    }

    [Benchmark]
    public void DrawWithoutTextMutation ()
    {
        _formatter.Draw (_driver, _drawRect, _normalAttribute, _hotAttribute, Rectangle.Empty);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        Application.Shutdown ();
    }
}
