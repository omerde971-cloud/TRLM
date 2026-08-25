namespace TRLM.Inventory
{
    /// <summary>One inventory slot. A null Item means the slot is empty.</summary>
    public struct InventorySlot
    {
        public ItemDefinition item;
        public int count;

        public bool IsEmpty => item == null || count <= 0;
    }
}
