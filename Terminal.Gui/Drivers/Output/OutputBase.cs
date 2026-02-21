using System.Collections.Concurrent;

namespace Terminal.Gui.Drivers;

/// <summary>
///     Abstract base class to assist with implementing <see cref="IOutput"/>.
/// </summary>
public abstract class OutputBase
{
    private bool _force16Colors;

    /// <inheritdoc cref="IOutput.Force16Colors"/>
    public bool Force16Colors
    {
        get => _force16Colors;
        set
        {
            if (IsLegacyConsole && !value)
            {
                return;
            }

            _force16Colors = value;
        }
    }

    private bool _isLegacyConsole;

    /// <inheritdoc cref="IOutput.IsLegacyConsole"/>
    public bool IsLegacyConsole
    {
        get => _isLegacyConsole;
        set
        {
            _isLegacyConsole = value;

            if (value) // If legacy console (true), force 16 colors
            {
                Force16Colors = true;
            }
        }
    }

    private readonly ConcurrentQueue<SixelToRender> _sixels = [];

    /// <inheritdoc cref="IOutput.GetSixels"/>>
    public ConcurrentQueue<SixelToRender> GetSixels () => _sixels;

    // Last text style used, for updating style with EscSeqUtils.CSI_AppendTextStyleChange().
    private TextStyle _redrawTextStyle = TextStyle.None;

    StringBuilder _lastOutputStringBuilder = new ();
    private bool _clearLastOutputPending;

    /// <summary>
    ///     Writes dirty cells from the buffer to the console. Hides cursor, iterates rows/cols,
    ///     skips clean cells, batches dirty cells into ANSI sequences, wraps URLs with OSC 8,
    ///     then renders sixel images. Cursor visibility is managed by <c>ApplicationMainLoop.SetCursor()</c>.
    /// </summary>
    public virtual void Write (IOutputBuffer buffer)
    {
        _clearLastOutputPending = true;
        if (buffer.Contents is null)
        {
            return;
        }

        StringBuilder outputStringBuilder = new ();
        int top = 0;
        int left = 0;
        int rows = buffer.Rows;
        int cols = buffer.Cols;
        Attribute? redrawAttr = null;
        OutputBufferImpl? concreteBuffer = buffer as OutputBufferImpl;
        bool []? dirtyLines = concreteBuffer?.DirtyLines;
        bool useDirtyLineFastPath = dirtyLines is { Length: > 0 };

        // Process each row
        for (int row = top; row < rows; row++)
        {
            if (useDirtyLineFastPath && row < dirtyLines!.Length && !dirtyLines [row])
            {
                bool rowHasDirtyCells = false;

                for (int colIndex = left; colIndex < cols; colIndex++)
                {
                    if (!buffer.Contents [row, colIndex].IsDirty)
                    {
                        continue;
                    }

                    rowHasDirtyCells = true;
                    break;
                }

                if (!rowHasDirtyCells)
                {
                    continue;
                }
            }

            int col = left;
            while (col < cols)
            {
                if (!buffer.Contents [row, col].IsDirty)
                {
                    col++;

                    continue;
                }

                int runStartCol = col;
                int outputWidth = 0;
                outputStringBuilder.Clear ();

                while (col < cols && buffer.Contents [row, col].IsDirty)
                {
                    int originalCol = col;
                    Cell cell = buffer.Contents [row, col];
                    buffer.Contents [row, col].IsDirty = false;
                    AppendCellAnsi (cell, outputStringBuilder, ref redrawAttr, ref _redrawTextStyle, cols, ref col, ref outputWidth);

                    if (col != originalCol && col < cols)
                    {
                        // Was a wide grapheme so mark clean next cell.
                        // See https://github.com/gui-cs/Terminal.Gui/issues/4466
                        buffer.Contents [row, col].IsDirty = false;
                    }

                    col++;
                }

                SetCursorPositionImpl (runStartCol, row);
                WriteToConsole (outputStringBuilder);
            }

            if (useDirtyLineFastPath && row < dirtyLines!.Length)
            {
                dirtyLines [row] = false;
            }
        }

        if (IsLegacyConsole)
        {
            return;
        }

        // Render queued sixel images
        foreach (SixelToRender s in GetSixels ())
        {
            if (string.IsNullOrWhiteSpace (s.SixelData))
            {
                continue;
            }

            SetCursorPositionImpl (s.ScreenPosition.X, s.ScreenPosition.Y);
            Write (new StringBuilder (s.SixelData));
        }
    }

    /// <inheritdoc cref="IOutput.GetLastOutput" />
    public virtual string GetLastOutput () => _lastOutputStringBuilder.ToString ();

    /// <summary>
    ///     Changes the color and text style of the console to the given <paramref name="attr"/> and
    ///     <paramref name="redrawTextStyle"/>.
    ///     If command can be buffered in line with other output (e.g. CSI sequence) then it should be appended to
    ///     <paramref name="output"/>
    ///     otherwise the relevant output state should be flushed directly (e.g. by calling relevant win 32 API method)
    /// </summary>
    /// <param name="output"></param>
    /// <param name="attr"></param>
    /// <param name="redrawTextStyle"></param>
    protected abstract void AppendOrWriteAttribute (StringBuilder output, Attribute attr, TextStyle redrawTextStyle);

    /// <summary>
    ///     When overriden in derived class, positions the terminal draw cursor to the specified point on the screen.
    ///     Note, this does NOT update any internal cursor position state - that is the responsibility of the caller.
    /// </summary>
    /// <param name="screenPositionX">Column to move cursor to</param>
    /// <param name="screenPositionY">Row to move cursor to</param>
    /// <returns></returns>
    protected abstract bool SetCursorPositionImpl (int screenPositionX, int screenPositionY);

    /// <summary>
    ///     Output the contents of the <paramref name="output"/> to the console.
    /// </summary>
    /// <param name="output"></param>
    protected virtual void Write (StringBuilder output)
    {
        if (_clearLastOutputPending)
        {
            _lastOutputStringBuilder.Clear ();
            _clearLastOutputPending = false;
        }

        _lastOutputStringBuilder.Append (output);
    }

    /// <summary>
    ///     Builds ANSI escape sequences for the specified rectangular region of the buffer.
    /// </summary>
    /// <param name="buffer">The output buffer to build ANSI for.</param>
    /// <param name="startRow">The starting row (inclusive).</param>
    /// <param name="endRow">The ending row (exclusive).</param>
    /// <param name="startCol">The starting column (inclusive).</param>
    /// <param name="endCol">The ending column (exclusive).</param>
    /// <param name="output">The StringBuilder to append ANSI sequences to.</param>
    /// <param name="lastAttr">The last attribute used, for optimization.</param>
    /// <param name="includeCellPredicate">Predicate to determine which cells to include. If null, includes all cells.</param>
    /// <param name="addNewlines">Whether to add newlines between rows.</param>
    protected void BuildAnsiForRegion (
        IOutputBuffer buffer,
        int startRow,
        int endRow,
        int startCol,
        int endCol,
        StringBuilder output,
        ref Attribute? lastAttr,
        Func<int, int, bool>? includeCellPredicate = null,
        bool addNewlines = true
    )
    {
        TextStyle redrawTextStyle = TextStyle.None;

        for (int row = startRow; row < endRow; row++)
        {
            for (int col = startCol; col < endCol; col++)
            {
                if (includeCellPredicate != null && !includeCellPredicate (row, col))
                {
                    continue;
                }

                Cell cell = buffer.Contents! [row, col];
                int outputWidth = -1;
                AppendCellAnsi (cell, output, ref lastAttr, ref redrawTextStyle, endCol, ref col, ref outputWidth);
            }

            // Add newline at end of row if requested
            if (addNewlines)
            {
                output.AppendLine ();
            }
        }
    }

    /// <summary>
    ///     Appends ANSI sequences for a single cell to the output.
    /// </summary>
    /// <param name="cell">The cell to append ANSI for.</param>
    /// <param name="output">The StringBuilder to append to.</param>
    /// <param name="lastAttr">The last attribute used, updated if the cell's attribute is different.</param>
    /// <param name="redrawTextStyle">The current text style for optimization.</param>
    /// <param name="maxCol">The maximum column, used for wide character handling.</param>
    /// <param name="currentCol">The current column, updated for wide characters.</param>
    /// <param name="outputWidth">The current output width, updated for wide characters.</param>
    protected void AppendCellAnsi (Cell cell, StringBuilder output, ref Attribute? lastAttr, ref TextStyle redrawTextStyle, int maxCol, ref int currentCol, ref int outputWidth)
    {
        Attribute? attribute = cell.Attribute;

        // Add ANSI escape sequence for attribute change
        if (attribute.HasValue && attribute.Value != lastAttr)
        {
            lastAttr = attribute.Value;
            AppendOrWriteAttribute (output, attribute.Value, redrawTextStyle);
            redrawTextStyle = attribute.Value.Style;
        }

        // Add the grapheme
        string grapheme = cell.Grapheme;
        output.Append (grapheme);
        outputWidth++;

        // Handle wide grapheme
        if (grapheme.GetColumns () > 1 && currentCol + 1 < maxCol)
        {
            currentCol++; // Skip next cell for wide character
            outputWidth++;
        }
    }

    /// <summary>
    ///     Generates an ANSI escape sequence string representation of the given <paramref name="buffer"/> contents.
    ///     This is the same output that would be written to the terminal to recreate the current screen contents.
    /// </summary>
    /// <param name="buffer">The output buffer to convert to ANSI.</param>
    /// <returns>A string containing ANSI escape sequences representing the buffer contents.</returns>
    public string ToAnsi (IOutputBuffer buffer)
    {
        // Legacy consoles don't support ANSI escape sequences
        // Return plain text representation instead
        if (IsLegacyConsole)
        {
            StringBuilder output = new ();

            for (int row = 0; row < buffer.Rows; row++)
            {
                for (int col = 0; col < buffer.Cols; col++)
                {
                    Cell cell = buffer.Contents! [row, col];
                    string grapheme = cell.Grapheme;
                    output.Append (grapheme);

                    // Handle wide grapheme
                    if (grapheme.GetColumns () > 1 && col + 1 < buffer.Cols)
                    {
                        col++; // Skip next cell for wide character
                    }
                }

                output.AppendLine ();
            }

            return output.ToString ();
        }

        StringBuilder ansiOutput = new ();
        Attribute? lastAttr = null;

        BuildAnsiForRegion (buffer, 0, buffer.Rows, 0, buffer.Cols, ansiOutput, ref lastAttr);

        return ansiOutput.ToString ();
    }

    /// <summary>
    ///     Writes buffered output to console, wrapping URLs with OSC 8 hyperlinks (non-legacy only),
    ///     when URL-like content is present.
    /// </summary>
    private void WriteToConsole (StringBuilder output)
    {
        if (IsLegacyConsole || !MayContainHyperlinkCandidate (output))
        {
            Write (output);
        }
        else
        {
            // Wrap URLs with OSC 8 hyperlink sequences
            StringBuilder processed = Osc8UrlLinker.WrapOsc8 (output);
            Write (processed);
        }
    }

    private static bool MayContainHyperlinkCandidate (StringBuilder output)
    {
        if (output.Length < 3)
        {
            return false;
        }

        for (int i = 0; i <= output.Length - 3; i++)
        {
            if (output [i] == ':' && output [i + 1] == '/' && output [i + 2] == '/')
            {
                return true;
            }
        }

        return false;
    }
}
