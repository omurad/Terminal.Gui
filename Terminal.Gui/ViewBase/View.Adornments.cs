namespace Terminal.Gui.ViewBase;

public partial class View // Adornments
{
    private Margin? _margin;
    private Border? _border;
    private Padding? _padding;

    /// <summary>
    ///     Initializes the Adornments of the View. Called by the constructor.
    /// </summary>
    private void SetupAdornments ()
    {
        // Keep eager object creation for now to preserve longstanding View invariants.
        // The constructor-mode + lazy input/command work still trims a large chunk of adornment overhead.
        if (this is not Adornment)
        {
            EnsureAdornmentsCreated ();
        }
    }

    private void InitializeAdornmentIfNeeded (Adornment adornment)
    {
        if (adornment.IsInitialized)
        {
            return;
        }

        if (_isInitializing || IsInitialized)
        {
            adornment.BeginInit ();
        }

        if (IsInitialized)
        {
            adornment.EndInit ();
        }
    }

    private void EnsureAdornmentsCreated ()
    {
        if (this is Adornment)
        {
            return;
        }

        bool createMargin = _margin is null;
        bool createBorder = _border is null;
        bool createPadding = _padding is null;

        if (!createMargin && !createBorder && !createPadding)
        {
            return;
        }

        _margin ??= new (this) { Enabled = Enabled };
        _border ??= new (this) { Enabled = Enabled };
        _padding ??= new (this) { Enabled = Enabled };

        SetAdornmentFrames ();

        if (createMargin)
        {
            InitializeAdornmentIfNeeded (_margin);
        }

        if (createBorder)
        {
            InitializeAdornmentIfNeeded (_border);
        }

        if (createPadding)
        {
            InitializeAdornmentIfNeeded (_padding);
        }
    }

    private Margin EnsureMargin ()
    {
        EnsureAdornmentsCreated ();
        return _margin!;
    }

    private Border EnsureBorder ()
    {
        EnsureAdornmentsCreated ();
        return _border!;
    }

    private Padding EnsurePadding ()
    {
        EnsureAdornmentsCreated ();
        return _padding!;
    }

    private void BeginInitAdornments ()
    {
        _margin?.BeginInit ();
        _border?.BeginInit ();
        _padding?.BeginInit ();
    }

    private void EndInitAdornments ()
    {
        _margin?.EndInit ();
        _border?.EndInit ();
        _padding?.EndInit ();
    }

    private void DisposeAdornments ()
    {
        _margin?.Dispose ();
        _margin = null;
        _border?.Dispose ();
        _border = null;
        _padding?.Dispose ();
        _padding = null;
    }

    /// <summary>
    ///     The <see cref="Adornment"/> that enables separation of a View from other SubViews of the same
    ///     SuperView. The margin offsets the <see cref="Viewport"/> from the <see cref="Frame"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The margin is typically transparent. This can be overriden by explicitly setting <see cref="Scheme"/>.
    ///     </para>
    ///     <para>
    ///         Enabling <see cref="ShadowStyle"/> will change the Thickness of the Margin to include the shadow.
    ///     </para>
    ///     <para>
    ///         The adornments (<see cref="Margin"/>, <see cref="Border"/>, and <see cref="Padding"/>) are not part of the
    ///         View's content and are not clipped by the View's Clip Area.
    ///     </para>
    ///     <para>
    ///         Changing the size of an adornment (<see cref="Margin"/>, <see cref="Border"/>, or <see cref="Padding"/>) will
    ///         change the size of <see cref="Frame"/> which will call <see cref="SetNeedsLayout"/> to update the layout of the
    ///         <see cref="SuperView"/> and its <see cref="SubViews"/>.
    ///     </para>
    /// </remarks>
    public Margin? Margin
    {
        get => this is Adornment ? null : EnsureMargin ();
        private set => _margin = value;
    }

    internal Margin? MarginOrNull => _margin;

    private ShadowStyle _shadowStyle;

    /// <summary>
    ///     Gets or sets whether the View is shown with a shadow effect. The shadow is drawn on the right and bottom sides of
    ///     the
    ///     Margin.
    /// </summary>
    /// <remarks>
    ///     Setting this property to <see langword="true"/> will add a shadow to the right and bottom sides of the Margin.
    ///     The View 's <see cref="Frame"/> will be expanded to include the shadow.
    /// </remarks>
    public virtual ShadowStyle ShadowStyle
    {
        get => _shadowStyle;
        set
        {
            if (_shadowStyle == value)
            {
                return;
            }

            _shadowStyle = value;

            if (this is Adornment)
            {
                return;
            }

            if (_margin is { } margin)
            {
                margin.ShadowStyle = value;
            }
        }
    }

    /// <summary>
    ///     The <see cref="Adornment"/> that offsets the <see cref="Viewport"/> from the <see cref="Margin"/>.
    ///     <para>
    ///         The Border provides the space for a visual border (drawn using
    ///         line-drawing glyphs) and the Title. The Border expands inward; in other words if `Border.Thickness.Top == 2`
    ///         the
    ///         border and title will take up the first row and the second row will be filled with spaces.
    ///     </para>
    ///     <para>
    ///         The Border provides the UI for mouse and keyboard arrangement of the View. See <see cref="Arrangement"/>.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para><see cref="BorderStyle"/> provides a simple helper for turning a simple border frame on or off.</para>
    ///     <para>
    ///         The adornments (<see cref="Margin"/>, <see cref="Border"/>, and <see cref="Padding"/>) are not part of the
    ///         View's content and are not clipped by the View's Clip Area.
    ///     </para>
    ///     <para>
    ///         Changing the size of an adornment (<see cref="Margin"/>, <see cref="Border"/>, or <see cref="Padding"/>) will
    ///         change the size of <see cref="Frame"/> which will call <see cref="SetNeedsLayout"/> to update the layout of the
    ///         <see cref="SuperView"/> and its <see cref="SubViews"/>.
    ///     </para>
    /// </remarks>
    public Border? Border
    {
        get => this is Adornment ? null : EnsureBorder ();
        private set => _border = value;
    }

    internal Border? BorderOrNull => _border;

    // TODO: Make BorderStyle nullable https://github.com/gui-cs/Terminal.Gui/issues/4021
    /// <summary>Gets or sets whether the view has a one row/col thick border.</summary>
    /// <remarks>
    ///     <para>
    ///         This is a helper for manipulating the view's <see cref="Border"/>. Setting this property to any value other
    ///         than <see cref="LineStyle.None"/> is equivalent to setting <see cref="Border"/>'s
    ///         <see cref="Adornment.Thickness"/> to `1` and <see cref="BorderStyle"/> to the value.
    ///     </para>
    ///     <para>
    ///         Setting this property to <see cref="LineStyle.None"/> is equivalent to setting <see cref="Border"/>'s
    ///         <see cref="Adornment.Thickness"/> to `0` and <see cref="BorderStyle"/> to <see cref="LineStyle.None"/>.
    ///     </para>
    ///     <para>
    ///         Raises <see cref="OnBorderStyleChanged"/> and raises <see cref="BorderStyleChanged"/>, which allows change
    ///         to be cancelled.
    ///     </para>
    ///     <para>For more advanced customization of the view's border, manipulate see <see cref="Border"/> directly.</para>
    /// </remarks>
    public LineStyle BorderStyle
    {
        get => _border?.LineStyle ?? LineStyle.Single;
        set
        {
            _ = EnsureBorder ();

            SetBorderStyle (value);
            OnBorderStyleChanged ();
            BorderStyleChanged?.Invoke (this, EventArgs.Empty);
        }
    }

    /// <summary>
    ///     Called when the <see cref="BorderStyle"/> has changed.
    /// </summary>
    protected virtual bool OnBorderStyleChanged () => false;

    /// <summary>
    ///     Fired when the <see cref="BorderStyle"/> has changed.
    /// </summary>
    public event EventHandler<EventArgs>? BorderStyleChanged;

    /// <summary>
    ///     Sets the <see cref="BorderStyle"/> of the view to the specified value.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="BorderStyle"/> is a helper for manipulating the view's <see cref="Border"/>. Setting this property
    ///         to any value other
    ///         than <see cref="LineStyle.None"/> is equivalent to setting <see cref="Border"/>'s
    ///         <see cref="Adornment.Thickness"/> to `1` and <see cref="BorderStyle"/> to the value.
    ///     </para>
    ///     <para>
    ///         Setting this property to <see cref="LineStyle.None"/> is equivalent to setting <see cref="Border"/>'s
    ///         <see cref="Adornment.Thickness"/> to `0` and <see cref="BorderStyle"/> to <see cref="LineStyle.None"/>.
    ///     </para>
    ///     <para>For more advanced customization of the view's border, manipulate see <see cref="Border"/> directly.</para>
    /// </remarks>
    /// <param name="style"></param>
    internal void SetBorderStyle (LineStyle style)
    {
        Border border = EnsureBorder ();

        if (style != LineStyle.None)
        {
            if (border.Thickness == Thickness.Empty)
            {
                border.Thickness = new (1);
            }
        }
        else
        {
            border.Thickness = new (0);
        }

        border.LineStyle = style;

        SetAdornmentFrames ();
        SetNeedsLayout ();
    }

    /// <summary>
    ///     The <see cref="Adornment"/> inside of the view that offsets the <see cref="Viewport"/>
    ///     from the <see cref="Border"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The adornments (<see cref="Margin"/>, <see cref="Border"/>, and <see cref="Padding"/>) are not part of the
    ///         View's content and are not clipped by the View's Clip Area.
    ///     </para>
    ///     <para>
    ///         Changing the size of an adornment (<see cref="Margin"/>, <see cref="Border"/>, or <see cref="Padding"/>) will
    ///         change the size of <see cref="Frame"/> which will call <see cref="SetNeedsLayout"/> to update the layout of the
    ///         <see cref="SuperView"/> and its <see cref="SubViews"/>.
    ///     </para>
    /// </remarks>
    public Padding? Padding
    {
        get => this is Adornment ? null : EnsurePadding ();
        private set => _padding = value;
    }

    internal Padding? PaddingOrNull => _padding;

    /// <summary>
    ///     <para>Gets the thickness describing the sum of the Adornments' thicknesses.</para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The <see cref="Viewport"/> is offset from the <see cref="Frame"/> by the thickness returned by this method.
    ///     </para>
    /// </remarks>
    /// <returns>A thickness that describes the sum of the Adornments' thicknesses.</returns>
    public Thickness GetAdornmentsThickness ()
    {
        var result = Thickness.Empty;

        if (_margin is { } margin)
        {
            result += margin.Thickness;
        }

        if (_border is { } border)
        {
            result += border.Thickness;
        }

        if (_padding is { } padding)
        {
            result += padding.Thickness;
        }

        return result;
    }

    /// <summary>Sets the Frame's of the Margin, Border, and Padding.</summary>
    internal void SetAdornmentFrames ()
    {
        if (this is Adornment)
        {
            // Adornments do not have Adornments
            return;
        }

        Margin? margin = _margin;
        Border? border = _border;
        Padding? padding = _padding;

        if (margin is { })
        {
            margin.Frame = Rectangle.Empty with { Size = Frame.Size };
        }

        if (border is { } && margin is { })
        {
            border.Frame = margin.Thickness.GetInside (margin.Frame);
        }

        if (padding is { } && border is { })
        {
            padding.Frame = border.Thickness.GetInside (border.Frame);
        }
    }
}
