using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private bool workspaceLayoutQueued;

    private void BindWorkspaceLayout()
    {
        preflightGrip.CurrentHeightDip = () => settings.WorkspaceHeights.PreflightDip;
        resultsGrip.CurrentHeightDip = () => settings.WorkspaceHeights.ResultsDip;
        preflightGrip.HeightRequested += height =>
        {
            settings = settings with { WorkspaceHeights = settings.WorkspaceHeights with { PreflightDip = height } };
            UpdateWorkspaceLayout();
        };
        resultsGrip.HeightRequested += height =>
        {
            settings = settings with { WorkspaceHeights = settings.WorkspaceHeights with { ResultsDip = height } };
            UpdateWorkspaceLayout();
        };
        foreach (var grid in new[] { preflight, results, manualGrid })
        {
            grid.RowHeightChanged += (_, _) => QueueWorkspaceLayout();
            grid.RowsAdded += (_, _) => QueueWorkspaceLayout();
            grid.RowsRemoved += (_, _) => QueueWorkspaceLayout();
            grid.ColumnWidthChanged += (_, _) => QueueWorkspaceLayout();
            grid.ColumnHeadersHeightChanged += (_, _) => QueueWorkspaceLayout();
        }
        workspaceViewport.SizeChanged += (_, _) => UpdateWorkspaceLayout();
        DpiChanged += (_, _) => BeginInvoke(UpdateWorkspaceLayout);
        Shown += (_, _) =>
        {
            var coordinator = new WorkspaceScrollCoordinator(this, workspaceViewport, [manualGrid, preflight, results]);
            Disposed += (_, _) => coordinator.Dispose();
        };
    }

    private void QueueWorkspaceLayout()
    {
        if (workspaceLayoutQueued || !IsHandleCreated || IsDisposed)
            return;
        workspaceLayoutQueued = true;
        BeginInvoke(() =>
        {
            workspaceLayoutQueued = false;
            UpdateWorkspaceLayout();
        });
    }

    private void UpdateWorkspaceLayout()
    {
        if (changingLayout || IsDisposed)
            return;
        changingLayout = true;
        try
        {
            var width = Math.Max(220, workspaceViewport.ClientSize.Width - settingsPanel.Padding.Horizontal - Dip(16));
            if (nxStatus.MaximumSize.Width != width)
                nxStatus.MaximumSize = new Size(width, 0);
            manualGrid.Height = Math.Max(Dip(132), MinimumGridHeight(manualGrid));
            preflight.Height = Math.Max(Dip(settings.WorkspaceHeights.PreflightDip), MinimumGridHeight(preflight));
            results.Height = Math.Max(Dip(settings.WorkspaceHeights.ResultsDip), MinimumGridHeight(results));
            preflightGrip.Height = resultsGrip.Height = Dip(15);
        }
        finally { changingLayout = false; }
    }

    private int Dip(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    private int MinimumGridHeight(Controls.WorkspaceGrid grid) => grid.MinimumHeightForWrappedRows(Font.Height + Dip(22));
}
