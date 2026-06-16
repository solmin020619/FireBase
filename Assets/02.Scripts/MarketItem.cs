[System.Serializable]
public class MarketItem
{
    public string SellerKey;
    public string ItemName;
    public int Price;
    public string Status;

    public MarketItem()
    {
    }

    public MarketItem(string sellerKey, string itemName, int price)
    {
        SellerKey = sellerKey;
        ItemName = itemName;
        Price = price;
        Status = "OnSale";
    }
}
