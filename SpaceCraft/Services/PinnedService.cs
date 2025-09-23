public class PinnedService
{
    private List<PinnedItem> _pinnedItems = new();

    public IReadOnlyList<PinnedItem> PinnedItems => _pinnedItems.AsReadOnly();

    public void AddPinnedItem(PinnedItem item) => _pinnedItems.Add(item);

    public void RemovePinnedItem(int id)
        => _pinnedItems.RemoveAll(x => x.Id == id);
}