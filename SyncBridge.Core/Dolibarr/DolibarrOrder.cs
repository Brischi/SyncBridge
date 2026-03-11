namespace SyncBridge.Core.Dolibarr
{
    public class DolibarrOrder
    {
        public string ExternalId { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal Total { get; set; }

        // wird einfach 1:1 durchgereicht
        public List<object> Items { get; set; } = new();
    }
}
