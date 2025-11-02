
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace WorkIntegrated
{
    public class TableDataRow
    {
        public TableDataRow(List<string> cells)
        {
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public List<string> Cells { get; }
    }

    public class TableData
    {
        public TableData(List<string> columnHeaders, List<TableDataRow> rows)
        {
            if (columnHeaders == null)
                throw new ArgumentNullException(nameof(columnHeaders));
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Cells.Count != columnHeaders.Count)
                    throw new ArgumentException("Row cell count must match column header count", nameof(rows));
            }

            ColumnHeaders = columnHeaders;
            Rows = rows;
        }

        public List<string> ColumnHeaders { get; }
        public List<TableDataRow> Rows { get; }
    }

    public static class DataGridHelper
    {
        private static void TableDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid && e.NewValue is TableData tableData)
            {
                dataGrid.Columns.Clear();

                for (int i = 0; i < tableData.ColumnHeaders.Count; i++)
                {
                    var column = new DataGridTextColumn
                    {
                        Binding = new Binding($"Cells[{i}]"),
                        Header = tableData.ColumnHeaders[i],
                    };
                    dataGrid.Columns.Add(column);
                }

                dataGrid.ItemsSource = tableData.Rows;
            }
        }

        public static TableData GetTableData(DependencyObject obj)
        {
            return (TableData)obj.GetValue(TableDataProperty);
        }

        public static void SetTableData(DependencyObject obj, TableData value)
        {
            obj.SetValue(TableDataProperty, value);
        }

        public static readonly DependencyProperty TableDataProperty =
            DependencyProperty.RegisterAttached(
                "TableData",
                typeof(TableData),
                typeof(DataGridHelper),
                new PropertyMetadata(null, TableDataChanged)
            );
    }

    //reference for creating dynamic tables
    //https://stackoverflow.com/questions/13106967/how-to-create-table-dynamically-in-c-sharp
}
