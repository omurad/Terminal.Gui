using BenchmarkDotNet.Attributes;
using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using TuiColor = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Benchmarks.Output;

[MemoryDiagnoser]
[BenchmarkCategory (nameof (OutputBase))]
public class OutputWriteBenchmark
{
    private const int SPARSE_ROW_STRIDE = 8;

    private AnsiOutput _output = null!;
    private OutputBufferImpl _buffer = null!;

    [Params (80, 200)]
    public int Width { get; set; }

    [Params (25, 60)]
    public int Height { get; set; }

    [GlobalSetup]
    public void Setup ()
    {
        _output = new ();
        _output.IsLegacyConsole = false;
        _output.SetSize (Width, Height);

        _buffer = new ();
        _buffer.SetSize (Width, Height);
    }

    [Benchmark (Baseline = true)]
    public void SparseDirtyFlush ()
    {
        PrepareSparseDirtyFrame ();
        _output.Write (_buffer);
    }

    [Benchmark]
    public void DenseDirtyFlush ()
    {
        PrepareDenseDirtyFrame ();
        _output.Write (_buffer);
    }

    [GlobalCleanup]
    public void Cleanup ()
    {
        _output.Dispose ();
    }

    private void PrepareSparseDirtyFrame ()
    {
        ClearDirtyState ();

        for (int row = 0; row < Height; row += SPARSE_ROW_STRIDE)
        {
            _buffer.Move (0, row);
            _buffer.AddStr ("Sparse");
        }
    }

    private void PrepareDenseDirtyFrame ()
    {
        _buffer.CurrentAttribute = new (TuiColor.White, TuiColor.Black);
        _buffer.FillRect (new Rectangle (0, 0, Width, Height), 'X');
    }

    private void ClearDirtyState ()
    {
        Cell [,] contents = _buffer.Contents!;

        for (int row = 0; row < Height; row++)
        {
            _buffer.DirtyLines [row] = false;

            for (int col = 0; col < Width; col++)
            {
                contents [row, col].IsDirty = false;
            }
        }

        _buffer.Move (0, 0);
    }
}
