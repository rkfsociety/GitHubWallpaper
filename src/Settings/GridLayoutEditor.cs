using GitHubWallpaper.Settings.Ui;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Редактор сетки репозиториев: размер сетки и перетаскивание между ячейками.
/// </summary>
internal sealed class GridLayoutEditor : UserControl
{
    private const int MinGridSize = 1;
    private const int MaxGridSize = 6;

    private readonly NumericUpDown _columnsUpDown;
    private readonly NumericUpDown _rowsUpDown;
    private readonly TableLayoutPanel _table;
    private readonly List<GridSlotPanel> _slotPanels = [];
    private string?[] _slots = [];
    private int? _dragSourceIndex;
    private bool _suppressEvents;
    private int _lastGridColumns = 3;
    private int _lastGridRows = 2;

    public GridLayoutEditor()
    {
        SettingsTheme.ApplySurfaceBackground(this);

        var sizePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            BackColor = SettingsTheme.BackgroundTop,
            Dock = DockStyle.Top,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 8),
        };
        SettingsTheme.EnableDoubleBuffer(sizePanel);

        var columnsLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0),
            Text = "Колонки:",
        };
        SettingsTheme.ApplyToLabel(columnsLabel, muted: true);
        sizePanel.Controls.Add(columnsLabel);

        _columnsUpDown = CreateGridSizeUpDown(3);
        _columnsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(_columnsUpDown);

        var rowsLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(16, 6, 8, 0),
            Text = "Строки:",
        };
        SettingsTheme.ApplyToLabel(rowsLabel, muted: true);
        sizePanel.Controls.Add(rowsLabel);

        _rowsUpDown = CreateGridSizeUpDown(2);
        _rowsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(_rowsUpDown);

        _table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.BackgroundTop,
            ColumnCount = 3,
            RowCount = 2,
            Dock = DockStyle.Top,
        };
        SettingsTheme.EnableDoubleBuffer(_table);

        Controls.Add(sizePanel);
        Controls.Add(_table);

        RebuildGrid();
        _lastGridColumns = GridColumns;
        _lastGridRows = GridRows;
    }

    public event EventHandler? LayoutChanged;

    public int GridColumns => (int)_columnsUpDown.Value;

    public int GridRows => (int)_rowsUpDown.Value;

    public int? SelectedSlotIndex { get; private set; }

    public void LoadLayout(int columns, int rows, IReadOnlyList<string> slots)
    {
        _suppressEvents = true;

        _columnsUpDown.Value = ClampGridSize(columns);
        _rowsUpDown.Value = ClampGridSize(rows);
        _slots = NormalizeSlots(slots, SlotCapacity);

        RebuildGrid();
        RefreshSlotPanels();

        _lastGridColumns = GridColumns;
        _lastGridRows = GridRows;
        _suppressEvents = false;
    }

    public IReadOnlyList<string> GetSlots() =>
        _slots.Select(slot => slot ?? string.Empty).ToList();

    public bool TryAddRepository(string slug)
    {
        var emptyIndex = Array.FindIndex(_slots, slot => string.IsNullOrWhiteSpace(slot));
        if (emptyIndex < 0 && !TryExpandColumnsByOne())
        {
            return false;
        }

        emptyIndex = Array.FindIndex(_slots, slot => string.IsNullOrWhiteSpace(slot));
        if (emptyIndex < 0)
        {
            return false;
        }

        _slots[emptyIndex] = slug;
        RefreshSlotPanels();
        RaiseLayoutChanged();
        return true;
    }

    public bool ContainsRepository(string slug) =>
        _slots.Any(slot => slot != null
            && slot.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public bool TryRemoveSelectedRepository()
    {
        if (SelectedSlotIndex is not int index
            || index < 0
            || index >= _slots.Length
            || string.IsNullOrWhiteSpace(_slots[index]))
        {
            return false;
        }

        _slots[index] = null;
        SelectedSlotIndex = null;
        RefreshSlotPanels();
        RaiseLayoutChanged();
        return true;
    }

    public int OccupiedSlotCount =>
        _slots.Count(slot => !string.IsNullOrWhiteSpace(slot));

    private int SlotCapacity => GridColumns * GridRows;

    private bool TryExpandColumnsByOne()
    {
        if (GridColumns >= MaxGridSize)
        {
            return false;
        }

        var occupied = CollectOccupiedRepos(_slots);
        var newColumns = GridColumns + 1;
        var newCapacity = newColumns * GridRows;

        _suppressEvents = true;
        _columnsUpDown.Value = newColumns;
        _suppressEvents = false;

        _slots = CompactSlots(occupied, newCapacity);
        RebuildGrid();
        _lastGridColumns = newColumns;
        _lastGridRows = GridRows;
        RefreshSlotPanels();
        return true;
    }

    private static NumericUpDown CreateGridSizeUpDown(decimal value)
    {
        var numeric = new NumericUpDown
        {
            Minimum = MinGridSize,
            Maximum = MaxGridSize,
            Width = 52,
            Value = value,
        };
        SettingsTheme.ApplyToNumeric(numeric);
        return numeric;
    }

    private static int ClampGridSize(int value) =>
        Math.Clamp(value, MinGridSize, MaxGridSize);

    private static string?[] NormalizeSlots(IReadOnlyList<string> slots, int capacity)
    {
        var normalized = new string?[capacity];

        for (var index = 0; index < capacity && index < slots.Count; index++)
        {
            var slug = slots[index]?.Trim();
            normalized[index] = string.IsNullOrEmpty(slug) ? null : slug;
        }

        return normalized;
    }

    private void OnGridSizeChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        var newColumns = (int)_columnsUpDown.Value;
        var newRows = (int)_rowsUpDown.Value;
        var newCapacity = newColumns * newRows;
        var occupied = CollectOccupiedRepos(_slots);

        if (occupied.Count > newCapacity)
        {
            _suppressEvents = true;
            _columnsUpDown.Value = _lastGridColumns;
            _rowsUpDown.Value = _lastGridRows;
            _suppressEvents = false;

            MessageBox.Show(
                $"В сетке {occupied.Count} репозиториев — при размере {newColumns}×{newRows} помещается только {newCapacity}.\n\n" +
                "Удалите лишние репозитории или выберите больший размер сетки.",
                FindForm()?.Text ?? "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _slots = CompactSlots(occupied, newCapacity);
        RebuildGrid();
        _lastGridColumns = newColumns;
        _lastGridRows = newRows;
        RefreshSlotPanels();
        RaiseLayoutChanged();
    }

    private static List<string> CollectOccupiedRepos(string?[] slots) =>
        slots
            .Where(slot => !string.IsNullOrWhiteSpace(slot))
            .Select(slot => slot!.Trim())
            .ToList();

    private static string?[] CompactSlots(IReadOnlyList<string> occupied, int capacity)
    {
        var compacted = new string?[capacity];

        for (var index = 0; index < occupied.Count && index < capacity; index++)
        {
            compacted[index] = occupied[index];
        }

        return compacted;
    }

    private void RebuildGrid()
    {
        _table.SuspendLayout();
        _table.Controls.Clear();
        _slotPanels.Clear();
        _table.ColumnStyles.Clear();
        _table.RowStyles.Clear();
        _table.ColumnCount = GridColumns;
        _table.RowCount = GridRows;

        for (var column = 0; column < GridColumns; column++)
        {
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / GridColumns));
        }

        for (var row = 0; row < GridRows; row++)
        {
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        }

        for (var index = 0; index < SlotCapacity; index++)
        {
            var panel = new GridSlotPanel(index)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
            };
            panel.MouseDown += OnSlotMouseDown;
            panel.DragEnter += OnSlotDragEnter;
            panel.DragDrop += OnSlotDragDrop;
            panel.Click += OnSlotClick;

            _slotPanels.Add(panel);
            _table.Controls.Add(panel, index % GridColumns, index / GridColumns);
        }

        _table.ResumeLayout();
        _slots = NormalizeSlots(_slots.Select(slot => slot ?? string.Empty).ToList(), SlotCapacity);
    }

    private void RefreshSlotPanels()
    {
        for (var index = 0; index < _slotPanels.Count; index++)
        {
            var slug = index < _slots.Length ? _slots[index] : null;
            _slotPanels[index].SetRepository(slug);
            _slotPanels[index].IsSelected = SelectedSlotIndex == index;
        }
    }

    private void OnSlotClick(object? sender, EventArgs e)
    {
        if (sender is not GridSlotPanel panel)
        {
            return;
        }

        SelectedSlotIndex = panel.SlotIndex;
        RefreshSlotPanels();
    }

    private void OnSlotMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || sender is not GridSlotPanel panel)
        {
            return;
        }

        SelectedSlotIndex = panel.SlotIndex;
        RefreshSlotPanels();

        if (string.IsNullOrWhiteSpace(panel.RepositorySlug))
        {
            return;
        }

        _dragSourceIndex = panel.SlotIndex;
        panel.DoDragDrop(panel.SlotIndex, DragDropEffects.Move);
        _dragSourceIndex = null;
    }

    private void OnSlotDragEnter(object? sender, DragEventArgs e)
    {
        if (sender is GridSlotPanel && e.Data?.GetDataPresent(typeof(int)) == true)
        {
            e.Effect = DragDropEffects.Move;
        }
    }

    private void OnSlotDragDrop(object? sender, DragEventArgs e)
    {
        if (sender is not GridSlotPanel target
            || e.Data?.GetData(typeof(int)) is not int sourceIndex
            || sourceIndex < 0
            || sourceIndex >= _slots.Length
            || target.SlotIndex < 0
            || target.SlotIndex >= _slots.Length)
        {
            return;
        }

        var destinationIndex = target.SlotIndex;
        if (sourceIndex == destinationIndex)
        {
            return;
        }

        (_slots[sourceIndex], _slots[destinationIndex]) =
            (_slots[destinationIndex], _slots[sourceIndex]);

        SelectedSlotIndex = destinationIndex;
        RefreshSlotPanels();
        RaiseLayoutChanged();
    }

    private void RaiseLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private sealed class GridSlotPanel : Panel
    {
        private readonly Label _label;
        private bool _selected;

        public GridSlotPanel(int slotIndex)
        {
            SlotIndex = slotIndex;
            AllowDrop = true;
            SettingsTheme.ApplySurfaceBackground(this);
            Padding = new Padding(8, 10, 8, 10);

            _label = new PassThroughLabel
            {
                AutoEllipsis = true,
                BackColor = Color.Transparent,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.75F),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Controls.Add(_label);
            SetRepository(null);
        }

        public int SlotIndex { get; }

        public string? RepositorySlug { get; private set; }

        public bool IsSelected
        {
            get => _selected;
            set
            {
                _selected = value;
                Invalidate();
            }
        }

        public void SetRepository(string? slug)
        {
            RepositorySlug = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();
            _label.Text = RepositorySlug ?? "— пусто —";
            _label.ForeColor = RepositorySlug is null
                ? SettingsTheme.SlotEmpty
                : SettingsTheme.TextPrimary;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;
            e.Graphics.SetClip(ClientRectangle);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
            using var fill = new SolidBrush(SettingsTheme.SlotFill);
            e.Graphics.FillPath(fill, path);

            var borderColor = _selected ? SettingsTheme.SlotSelected : SettingsTheme.GlassBorder;
            var borderWidth = _selected ? 2f : 1f;
            using var border = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(border, path);

            if (_selected)
            {
                using var glow = new SolidBrush(Color.FromArgb(36, SettingsTheme.Accent));
                using var glowPath = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
                e.Graphics.FillPath(glow, glowPath);
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    /// <summary>Метка, пропускающая мышь к родителю (для drag-and-drop по всей ячейке).</summary>
    private sealed class PassThroughLabel : Label
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                m.Result = (IntPtr)HtTransparent;
                return;
            }

            base.WndProc(ref m);
        }
    }
}
