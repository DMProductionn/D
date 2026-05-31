namespace ShoeStoreApp.Models
{
    internal class ProductRecord
    {
        public int ProductId { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public string Supplier { get; set; }
        public decimal Price { get; set; }
        public string UnitOfMeasure { get; set; }
        public int Quantity { get; set; }
        public decimal Discount { get; set; }
        public string PhotoPath { get; set; }
    }
}
