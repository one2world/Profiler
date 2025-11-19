namespace Unity.MemoryProfiler.UI.Services.SelectionDetails
{
    internal interface ISelectionDetailsPresenter
    {
        bool CanPresent(in SelectionDetailsContext context);

        void Present(in SelectionDetailsContext context);
    }
}
