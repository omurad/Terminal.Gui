using BenchmarkDotNet.Attributes;
using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;

namespace Terminal.Gui.Benchmarks.Output;

[MemoryDiagnoser]
[BenchmarkCategory (nameof (OutputBufferImpl))]
public class OutputBufferWriteBenchmark
{
    private const string ASCII_TEXT =
        "The quick brown fox jumps over the lazy dog. 0123456789. The quick brown fox jumps over the lazy dog.";

    private const string COMBINING_TEXT =
        "Cafe\u0301 nai\u0308ve co\u0327operate 👩‍💻 Z͑͘a͠l͡g̴o͠";

    private OutputBufferImpl _buffer = null!;
    private Rectangle _fullRect;

    [Params (120)]
    public int Width { get; set; }

    [Params (40)]
    public int Height { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _buffer = new ();
        _buffer.SetSize (Width, Height);
        _fullRect = new Rectangle (0, 0, Width, Height);
    }

    [IterationSetup]
    public void IterationSetup ()
    {
        _buffer.ClearContents ();
        _buffer.Move (0, 0);
    }

    [Benchmark (Baseline = true)]
    public void AddStrAscii ()
    {
        _buffer.Move (0, 0);
        _buffer.AddStr (ASCII_TEXT);
    }

    [Benchmark]
    public void AddStrCombining ()
    {
        _buffer.Move (0, 1);
        _buffer.AddStr (COMBINING_TEXT);
    }

    [Benchmark]
    public void FillRectClear ()
    {
        _buffer.FillRect (_fullRect, ' ');
    }
}
