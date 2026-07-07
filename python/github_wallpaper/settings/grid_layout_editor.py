"""Редактор сетки репозиториев (упрощённый порт GridLayoutEditor.cs)."""

from __future__ import annotations

from PySide6.QtCore import Qt, Signal
from PySide6.QtWidgets import (
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QSpinBox,
    QTableWidget,
    QTableWidgetItem,
    QVBoxLayout,
    QWidget,
)

_MIN_GRID_SIZE = 1
_MAX_GRID_SIZE = 6


class GridLayoutEditor(QWidget):
    """Размер сетки и ячейки с репозиториями."""

    layout_changed = Signal()

    def __init__(self, parent: QWidget | None = None) -> None:
        super().__init__(parent)
        self._suppress_events = False
        self._selected_row = -1
        self._selected_col = -1
        self._slots: list[str] = []

        self._columns_spin = QSpinBox()
        self._columns_spin.setRange(_MIN_GRID_SIZE, _MAX_GRID_SIZE)
        self._columns_spin.setValue(3)
        self._columns_spin.valueChanged.connect(self._on_grid_size_changed)

        self._rows_spin = QSpinBox()
        self._rows_spin.setRange(_MIN_GRID_SIZE, _MAX_GRID_SIZE)
        self._rows_spin.setValue(2)
        self._rows_spin.valueChanged.connect(self._on_grid_size_changed)

        size_row = QHBoxLayout()
        size_row.addWidget(QLabel("Колонки:"))
        size_row.addWidget(self._columns_spin)
        size_row.addSpacing(12)
        size_row.addWidget(QLabel("Строки:"))
        size_row.addWidget(self._rows_spin)
        size_row.addStretch(1)

        self._table = QTableWidget()
        self._table.setSelectionMode(QTableWidget.SelectionMode.SingleSelection)
        self._table.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        self._table.verticalHeader().setDefaultSectionSize(30)
        self._table.cellClicked.connect(self._on_cell_clicked)

        root = QVBoxLayout(self)
        root.addLayout(size_row)
        root.addWidget(self._table)

        self._rebuild_table()

    @property
    def grid_columns(self) -> int:
        return self._columns_spin.value()

    @property
    def grid_rows(self) -> int:
        return self._rows_spin.value()

    @property
    def occupied_slot_count(self) -> int:
        return sum(1 for slug in self._slots if slug)

    def get_slots(self) -> list[str]:
        return list(self._slots)

    def set_layout(self, columns: int, rows: int, slots: list[str]) -> None:
        self._suppress_events = True
        self._columns_spin.setValue(max(_MIN_GRID_SIZE, min(_MAX_GRID_SIZE, columns)))
        self._rows_spin.setValue(max(_MIN_GRID_SIZE, min(_MAX_GRID_SIZE, rows)))
        self._resize_slots()
        for index, slug in enumerate(slots):
            if index < len(self._slots):
                self._slots[index] = slug.strip() if slug else ""
        self._refresh_table()
        self._suppress_events = False

    def contains_repository(self, slug: str) -> bool:
        normalized = slug.strip().lower()
        return any(item.lower() == normalized for item in self._slots if item)

    def try_add_repository(self, slug: str) -> bool:
        trimmed = slug.strip()
        if not trimmed:
            return False

        for index, item in enumerate(self._slots):
            if not item:
                self._slots[index] = trimmed
                self._refresh_table()
                self._emit_layout_changed()
                return True
        return False

    def try_remove_selected_repository(self) -> bool:
        index = self._selected_index()
        if index < 0 or not self._slots[index]:
            return False
        self._slots[index] = ""
        self._refresh_table()
        self._emit_layout_changed()
        return True

    def _on_grid_size_changed(self) -> None:
        if self._suppress_events:
            return
        self._resize_slots()
        self._refresh_table()
        self._emit_layout_changed()

    def _on_cell_clicked(self, row: int, column: int) -> None:
        self._selected_row = row
        self._selected_col = column

    def _selected_index(self) -> int:
        if self._selected_row < 0 or self._selected_col < 0:
            return -1
        return self._selected_row * self.grid_columns + self._selected_col

    def _capacity(self) -> int:
        return self.grid_columns * self.grid_rows

    def _resize_slots(self) -> None:
        capacity = self._capacity()
        if len(self._slots) < capacity:
            self._slots.extend([""] * (capacity - len(self._slots)))
        elif len(self._slots) > capacity:
            overflow = [slug for slug in self._slots[capacity:] if slug]
            self._slots = self._slots[:capacity]
            for slug in overflow:
                placed = False
                for index, item in enumerate(self._slots):
                    if not item:
                        self._slots[index] = slug
                        placed = True
                        break
                if not placed:
                    break

    def _rebuild_table(self) -> None:
        self._resize_slots()
        self._refresh_table()

    def _refresh_table(self) -> None:
        columns = self.grid_columns
        rows = self.grid_rows
        self._table.clear()
        self._table.setColumnCount(columns)
        self._table.setRowCount(rows)
        self._table.setHorizontalHeaderLabels([f"C{index + 1}" for index in range(columns)])
        self._table.setVerticalHeaderLabels([f"R{index + 1}" for index in range(rows)])

        for row in range(rows):
            for col in range(columns):
                index = row * columns + col
                slug = self._slots[index] if index < len(self._slots) else ""
                item = QTableWidgetItem(slug or "— пусто —")
                if not slug:
                    item.setForeground(Qt.GlobalColor.gray)
                self._table.setItem(row, col, item)

        self._table.resizeColumnsToContents()
        row_height = self._table.verticalHeader().defaultSectionSize()
        header = self._table.horizontalHeader().height()
        self._table.setFixedHeight(rows * row_height + header + 6)

    def _emit_layout_changed(self) -> None:
        if not self._suppress_events:
            self.layout_changed.emit()
