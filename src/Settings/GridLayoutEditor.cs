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

    public GridLayoutEditor()
    {
        var sizePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 8),
        };

        sizePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0),
            Text = "Колонки:",
        });

        _columnsUpDown = CreateGridSizeUpDown(3);
        _columnsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(_columnsUpDown);

        sizePanel.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(16, 6, 8, 0),
            Text = "Строки:",
        });

        _rowsUpDown = CreateGridSizeUpDown(2);
        _rowsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(_rowsUpDown);

        _table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 2,
            Dock = DockStyle.Top,
        };

        Controls.Add(sizePanel);
        Controls.Add(_table);

        RebuildGrid();
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

        _suppressEvents = false;
    }

    public IReadOnlyList<string> GetSlots() =>
        _slots.Select(slot => slot ?? string.Empty).ToList();

    public bool TryAddRepository(string slug)
    {
        var emptyIndex = Array.FindIndex(_slots, slot => string.IsNullOrWhiteSpace(slot));
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

    private static NumericUpDown CreateGridSizeUpDown(decimal value) =>
        new()
        {
            Minimum = MinGridSize,
            Maximum = MaxGridSize,
            Width = 52,
            Value = value,
        };

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

        var previous = _slots.Select(slot => slot ?? string.Empty).ToList();
        RebuildGrid();
        _slots = NormalizeSlots(previous, SlotCapacity);
        RefreshSlotPanels();
        RaiseLayoutChanged();
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
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
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

        public GridSlotPanel(int slotIndex)
        {
            SlotIndex = slotIndex;
            AllowDrop = true;
            BackColor = Color.FromArgb(34, 39, 46);
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(6, 8, 6, 8);

            _label = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.Gainsboro,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Controls.Add(_label);
            SetRepository(null);
        }

        public int SlotIndex { get; }

        public string? RepositorySlug { get; private set; }

        public bool IsSelected
        {
            get => BorderStyle == BorderStyle.Fixed3D;
            set => BorderStyle = value ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
        }

        public void SetRepository(string? slug)
        {
            RepositorySlug = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim();
            _label.Text = RepositorySlug ?? "— пусто —";
            _label.ForeColor = RepositorySlug is null ? Color.Gray : Color.Gainsboro;
        }
    }
}
