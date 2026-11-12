using NxDrawingPdfExporter.App.Layout;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
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
        workspaceViewport.SizeChanged += (_, _) => UpdateWorkspaceLayout();
        DpiChanged += (_, _) => BeginInvoke(UpdateWorkspaceLayout);
        Shown += (_, _) =>
        {
            var coordinator = new WorkspaceScrollCoordinator(this, workspaceViewport, [manualGrid, preflight, results]);
            Disposed += (_, _) => coordinator.Dispose();
        };
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
    private int MinimumGridHeight(DataGridView grid) => grid.ColumnHeadersHeight + Font.Height + Dip(22);
}
