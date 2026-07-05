using GitHubWallpaper.Settings.Ui;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Редактор сетки репозиториев: размер сетки и перетаскивание между ячейками.
/// </summary>
internal sealed class GridLayoutEditor : UserControl
{
    private const int MinGridSize = 1;
    private const int MaxGridSize = 6;
    public const int DefaultSlotRowHeight = 48;
    public const int MinSlotRowHeight = 24;

    private readonly NumericUpDown _columnsUpDown;
    private readonly NumericUpDown _rowsUpDown;
    private readonly TableLayoutPanel _table;
    private readonly List<GridSlotPanel> _slotPanels = [];
    private string?[] _slots = [];
    private int? _dragSourceIndex;
    private bool _suppressEvents;
    private int _lastGridColumns = 3;
    private int _lastGridRows = 2;
    private int _slotRowHeight = DefaultSlotRowHeight;

    public GridLayoutEditor()
    {
        SettingsTheme.ApplyCardContentBackground(this);

        var sizePanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0, 4, 0, 0),
            RowCount = 1,
        };
        SettingsTheme.ApplyCardContentBackground(sizePanel);
        sizePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));
        sizePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sizePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sizePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sizePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var columnsLabel = CreateGridSizeLabel("Колонки:");
        sizePanel.Controls.Add(columnsLabel, 0, 0);

        _columnsUpDown = CreateGridSizeUpDown(3);
        _columnsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(new NumericField(_columnsUpDown)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 16, 0),
        }, 1, 0);

        var rowsLabel = CreateGridSizeLabel("Строки:");
        sizePanel.Controls.Add(rowsLabel, 2, 0);

        _rowsUpDown = CreateGridSizeUpDown(2);
        _rowsUpDown.ValueChanged += OnGridSizeChanged;
        sizePanel.Controls.Add(new NumericField(_rowsUpDown) { Dock = DockStyle.Fill }, 3, 0);

        _table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            ColumnCount = 3,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
        };
        _table.BackColor = SettingsTheme.InnerPanelFill;

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            RowCount = 2,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        SettingsTheme.ApplyCardContentBackground(root);

        root.Controls.Add(sizePanel, 0, 0);
        root.Controls.Add(_table, 0, 1);
        Controls.Add(root);

        RebuildGrid();
        _lastGridColumns = GridColumns;
        _lastGridRows = GridRows;
    }

    public event EventHandler? LayoutChanged;

    public int GridColumns => (int)_columnsUpDown.Value;

    public int GridRows => (int)_rowsUpDown.Value;

    public int SlotRowHeight => _slotRowHeight;

    public int? SelectedSlotIndex { get; private set; }

    public void SetSlotRowHeight(int height)
    {
        var clamped = Math.Clamp(height, MinSlotRowHeight, DefaultSlotRowHeight);
        if (_slotRowHeight == clamped)
        {
            return;
        }

        _slotRowHeight = clamped;
        ApplyRowHeights();
    }

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

    private static Label CreateGridSizeLabel(string text)
    {
        var label = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        SettingsTheme.ApplyToLabel(label, muted: true);
        return label;
    }

    private static NumericUpDown CreateGridSizeUpDown(decimal value)
    {
        var numeric = new ThemedNumericUpDown
        {
            Minimum = MinGridSize,
            Maximum = MaxGridSize,
            Value = value,
        };
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
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, _slotRowHeight));
        }

        for (var index = 0; index < SlotCapacity; index++)
        {
            var panel = new GridSlotPanel(index)
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
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

    private void ApplyRowHeights()
    {
        if (_table.RowCount == 0)
        {
            return;
        }

        for (var row = 0; row < GridRows; row++)
        {
            _table.RowStyles[row] = new RowStyle(SizeType.Absolute, _slotRowHeight);
        }

        PerformLayout();
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
            BackColor = SettingsTheme.InnerPanelFill;
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
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var hostColor = Parent?.BackColor ?? SettingsTheme.InnerPanelFill;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            using (var hostFill = new SolidBrush(hostColor))
            {
                e.Graphics.FillRectangle(hostFill, bounds);
            }

            bounds.Width -= 1;
            bounds.Height -= 1;
            using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
            using var fill = new SolidBrush(SettingsTheme.SlotFill);
            e.Graphics.FillPath(fill, path);

            var borderColor = _selected ? SettingsTheme.SlotSelected : SettingsTheme.CardBorder;
            var borderWidth = _selected ? 2f : 1f;
            using var border = new Pen(borderColor, borderWidth);
            e.Graphics.DrawPath(border, path);

            if (_selected)
            {
                using var glow = new SolidBrush(Color.FromArgb(36, SettingsTheme.Accent));
                e.Graphics.FillPath(glow, path);
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
