using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 심화 3. 거래소 / 경매장 기능
public class MarketManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-18018-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI - 코인 / 메시지")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;

    [Header("UI - 판매 등록")]
    [SerializeField] InputField SellItemNameInput;
    [SerializeField] InputField SellPriceInput;

    [Header("UI - 목록 / 구매")]
    [SerializeField] Text ListingsText;
    [SerializeField] InputField BuyIndexInput;

    string userKey;
    readonly List<string> marketKeys = new List<string>();

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            MessageText.text = "로그인 정보가 없습니다.";
            return;
        }

        RefreshCoin();
        LoadListings();
    }

    void RefreshCoin()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Coin")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result.Value == null)
                {
                    return;
                }

                int coin = int.Parse(task.Result.Value.ToString());

                dispatcher.Enqueue(() =>
                {
                    CoinText.text = "Coin : " + coin;
                });
            });
    }

    // 판매 등록 버튼에 연결
    public void OnClickListItem()
    {
        string itemName = SellItemNameInput.text.Trim();
        int.TryParse(SellPriceInput.text, out int price);

        if (string.IsNullOrEmpty(itemName) || price <= 0)
        {
            MessageText.text = "아이템 이름과 가격을 올바르게 입력하세요.";
            return;
        }

        ListItem(itemName, price);
    }

    void ListItem(string itemName, int price)
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 불러오기 실패";
                    });
                    return;
                }

                string inventoryJson = task.Result.Value.ToString();
                Dictionary<string, int> inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = itemName + " 개수가 부족합니다.";
                    });
                    return;
                }

                inventory[itemName]--;

                reference
                    .Child("UserInfo")
                    .Child(userKey)
                    .Child("Inventory")
                    .SetValueAsync(JsonConvert.SerializeObject(inventory))
                    .ContinueWith(invTask =>
                    {
                        if (invTask.IsFaulted)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                MessageText.text = "판매 등록 실패";
                            });
                            return;
                        }

                        MarketItem newItem = new MarketItem(userKey, itemName, price);
                        string json = JsonConvert.SerializeObject(newItem);

                        DatabaseReference newRef = reference.Child("Market").Push();

                        newRef.SetRawJsonValueAsync(json).ContinueWith(marketTask =>
                        {
                            if (marketTask.IsFaulted)
                            {
                                dispatcher.Enqueue(() =>
                                {
                                    MessageText.text = "판매 등록 실패";
                                });
                                return;
                            }

                            dispatcher.Enqueue(() =>
                            {
                                MessageText.text = itemName + " 판매 등록 완료!";
                            });

                            LoadListings();
                        });
                    });
            });
    }

    // 목록 새로고침 버튼에 연결
    public void OnClickRefreshListings()
    {
        LoadListings();
    }

    void LoadListings()
    {
        reference
            .Child("Market")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "거래소 목록 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                List<string> keys = new List<string>();
                List<MarketItem> items = new List<MarketItem>();

                if (snapshot.Exists)
                {
                    foreach (DataSnapshot child in snapshot.Children)
                    {
                        MarketItem item = JsonConvert.DeserializeObject<MarketItem>(child.GetRawJsonValue());

                        if (item.Status != "OnSale")
                        {
                            continue;
                        }

                        keys.Add(child.Key);
                        items.Add(item);
                    }
                }

                dispatcher.Enqueue(() =>
                {
                    marketKeys.Clear();
                    marketKeys.AddRange(keys);

                    string text = "";

                    for (int i = 0; i < items.Count; i++)
                    {
                        text += "[" + i + "] " + items[i].ItemName + " - " + items[i].Price + " Coin\n";
                    }

                    if (items.Count == 0)
                    {
                        text = "등록된 판매 아이템이 없습니다.";
                    }

                    ListingsText.text = text;
                });
            });
    }

    // 구매 버튼에 연결
    public void OnClickBuyListing()
    {
        if (!int.TryParse(BuyIndexInput.text, out int index) || index < 0 || index >= marketKeys.Count)
        {
            MessageText.text = "잘못된 번호입니다.";
            return;
        }

        BuyListing(marketKeys[index]);
    }

    void BuyListing(string marketKey)
    {
        reference
            .Child("Market")
            .Child(marketKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "판매 정보를 찾을 수 없습니다.";
                    });
                    return;
                }

                MarketItem listing = JsonConvert.DeserializeObject<MarketItem>(task.Result.GetRawJsonValue());

                if (listing.Status != "OnSale")
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "이미 판매가 완료된 아이템입니다.";
                        LoadListings();
                    });
                    return;
                }

                if (listing.SellerKey == userKey)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "본인이 등록한 아이템은 구매할 수 없습니다.";
                    });
                    return;
                }

                BuyListingFromBuyer(marketKey, listing);
            });
    }

    void BuyListingFromBuyer(string marketKey, MarketItem listing)
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "내 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot buyerSnapshot = task.Result;

                int buyerCoin = int.Parse(buyerSnapshot.Child("Coin").Value.ToString());

                if (buyerCoin < listing.Price)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "코인이 부족합니다.";
                    });
                    return;
                }

                string inventoryJson = buyerSnapshot.Child("Inventory").Value.ToString();
                Dictionary<string, int> buyerInventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                if (buyerInventory.ContainsKey(listing.ItemName))
                {
                    buyerInventory[listing.ItemName]++;
                }
                else
                {
                    buyerInventory[listing.ItemName] = 1;
                }

                int newBuyerCoin = buyerCoin - listing.Price;

                Dictionary<string, object> buyerUpdate = new Dictionary<string, object>();
                buyerUpdate["Coin"] = newBuyerCoin;
                buyerUpdate["Inventory"] = JsonConvert.SerializeObject(buyerInventory);

                reference
                    .Child("UserInfo")
                    .Child(userKey)
                    .UpdateChildrenAsync(buyerUpdate)
                    .ContinueWith(buyerTask =>
                    {
                        if (buyerTask.IsFaulted)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                MessageText.text = "구매 처리 실패";
                            });
                            return;
                        }

                        PaySeller(marketKey, listing);
                    });
            });
    }

    void PaySeller(string marketKey, MarketItem listing)
    {
        reference
            .Child("UserInfo")
            .Child(listing.SellerKey)
            .Child("Coin")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.Result.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "판매자 정보 불러오기 실패";
                    });
                    return;
                }

                int sellerCoin = int.Parse(task.Result.Value.ToString());
                int newSellerCoin = sellerCoin + listing.Price;

                reference
                    .Child("UserInfo")
                    .Child(listing.SellerKey)
                    .Child("Coin")
                    .SetValueAsync(newSellerCoin)
                    .ContinueWith(sellerTask =>
                    {
                        if (sellerTask.IsFaulted)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                MessageText.text = "판매자 코인 갱신 실패";
                            });
                            return;
                        }

                        CompletePurchase(marketKey, listing);
                    });
            });
    }

    void CompletePurchase(string marketKey, MarketItem listing)
    {
        reference
            .Child("Market")
            .Child(marketKey)
            .Child("Status")
            .SetValueAsync("Sold")
            .ContinueWith(task =>
            {
                dispatcher.Enqueue(() =>
                {
                    MessageText.text = listing.ItemName + " 구매 완료!";
                });

                RefreshCoin();
                LoadListings();
            });
    }
}
