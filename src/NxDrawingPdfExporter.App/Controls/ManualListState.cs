namespace NxDrawingPdfExporter.App;

internal sealed class ManualListState
{
    public GuiInputMode Mode { get; private set; }
    public bool Expanded { get; private set; }
    public bool CanEdit => Mode == GuiInputMode.ManualSelection;

    public void Select(GuiInputMode mode)
    {
        if (Mode == mode)
            return;
        Mode = mode;
        Expanded = CanEdit;
    }

    public void Toggle() => Expanded = !Expanded;
}
