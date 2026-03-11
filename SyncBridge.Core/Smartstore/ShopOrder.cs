namespace SyncBridge.Core.Smartstore
{
    public class ShopOrder
    {
        public int Id { get; set; }
        public DateTime CreatedOnUtc { get; set; }
        public decimal OrderTotal { get; set; }

        public List<object> Items { get; set; } = new();
    }
}
