using BenchmarkDotNet.Attributes;
using System.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Benchmarks.Layout;

[MemoryDiagnoser]
[BenchmarkCategory ("LayoutDependencies")]
public class LayoutDependencyBenchmark
{
    private readonly Size _contentSize = new (4000, 20);
    private View _root = null!;

    [Params (100, 500, 1000)]
    public int SubViewCount { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _root = new ()
        {
            Width = _contentSize.Width,
            Height = _contentSize.Height
        };

        View? previous = null;

        for (int i = 0; i < SubViewCount; i++)
        {
            Label label = new ()
            {
                Y = 0,
                Width = 1,
                Height = 1,
                Text = "X"
            };

            label.X = previous is null ? 0 : Pos.Right (previous);

            _root.Add (label);
            previous = label;
        }

        _root.Layout (_contentSize);
    }

    [Benchmark]
    public void LayoutDependencyChain ()
    {
        _root.SetNeedsLayout ();
        _root.Layout (_contentSize);
    }
}
