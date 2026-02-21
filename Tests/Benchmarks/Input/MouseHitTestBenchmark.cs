using BenchmarkDotNet.Attributes;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Terminal.Gui.Benchmarks.Input;

[MemoryDiagnoser]
[BenchmarkCategory ("MouseHitTest")]
public class MouseHitTestBenchmark
{
    private Runnable _root = null!;
    private Point _probePoint;

    [Params (5, 10)]
    public int Depth { get; set; }

    [Params (20, 80)]
    public int Breadth { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        Application.Init (driverName: "ANSI");

        _root = new Runnable ()
        {
            X = 0,
            Y = 0,
            Width = 220,
            Height = 80
        };

        PopulateWideAndDeepHierarchy (_root, Breadth, Depth);
        Application.Begin (_root);

        _probePoint = new Point (20, 10);
    }

    [Benchmark]
    public int GetViewsUnderLocation ()
    {
        List<View?> views = _root.GetViewsUnderLocation (_probePoint, ViewportSettingsFlags.TransparentMouse);
        return views.Count;
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        Application.Shutdown ();
    }

    private static void PopulateWideAndDeepHierarchy (View root, int breadth, int depth)
    {
        for (int i = 0; i < breadth; i++)
        {
            View current = new ()
            {
                X = i % 10 * 20,
                Y = i / 10 * 2,
                Width = 18,
                Height = 2,
                Id = $"top-{i}"
            };

            root.Add (current);

            for (int level = 1; level < depth; level++)
            {
                View nested = new ()
                {
                    X = 1,
                    Y = 0,
                    Width = 16,
                    Height = 1,
                    Id = $"n-{i}-{level}"
                };

                current.Add (nested);
                current = nested;
            }
        }
    }
}
