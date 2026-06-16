using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-18018-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text CoinText;
    [SerializeField] Text MessageText;

    string userKey;
    int currentCoin;
    Dictionary<string, int> inventory = new Dictionary<string, int>();
    Dictionary<string, bool> unitList = new Dictionary<string, bool>();

    readonly Dictionary<string, int> unitPrices = new Dictionary<string, int>
    {
        { "Unit2", 500 },
        { "Unit3", 1000 },
        { "Unit4", 2000 },
    };

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

        LoadUserData();
    }

    void LoadUserData()
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
                        MessageText.text = "유저 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());

                string inventoryJson = snapshot.Child("Inventory").Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                string unitListJson = snapshot.Child("UnitList").Value.ToString();
                unitList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(unitListJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "유저 정보 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        CoinText.text = "Coin : " + currentCoin;
    }

    public void OnClickBuyPistol()
    {
        BuyItem("Pistol", 100);
    }

    public void OnClickBuyShotgun()
    {
        BuyItem("Shotgun", 250);
    }

    public void OnClickBuySniperRifle()
    {
        BuyItem("SniperRifle", 500);
    }

    public void OnClickBuyRocketLauncher()
    {
        BuyItem("RocketLauncher", 1000);
    }

    void BuyItem(string itemName, int price)
    {
        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        currentCoin -= price;

        if (inventory.ContainsKey(itemName))
        {
            inventory[itemName]++;
        }
        else
        {
            inventory[itemName] = 1;
        }

        SaveUserData(itemName);
    }

    void SaveUserData(string boughtItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["Inventory"] = inventoryJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtItemName + " 구매 완료";
                });
            });
    }

    // 심화 1. 유닛 구매 기능
    public void OnClickBuyUnit2()
    {
        BuyUnit("Unit2");
    }

    public void OnClickBuyUnit3()
    {
        BuyUnit("Unit3");
    }

    public void OnClickBuyUnit4()
    {
        BuyUnit("Unit4");
    }

    void BuyUnit(string unitName)
    {
        int price = unitPrices[unitName];

        if (unitList.ContainsKey(unitName) && unitList[unitName])
        {
            MessageText.text = "이미 보유한 유닛입니다.";
            return;
        }

        if (currentCoin < price)
        {
            MessageText.text = "코인이 부족합니다.";
            return;
        }

        currentCoin -= price;
        unitList[unitName] = true;

        SaveUnitData(unitName);
    }

    void SaveUnitData(string boughtUnitName)
    {
        string unitListJson = JsonConvert.SerializeObject(unitList);

        Dictionary<string, object> updateData = new Dictionary<string, object>();
        updateData["Coin"] = currentCoin;
        updateData["UnitList"] = unitListJson;

        reference
            .Child("UserInfo")
            .Child(userKey)
            .UpdateChildrenAsync(updateData)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "유닛 구매 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = boughtUnitName + " 구매 완료";
                });
            });
    }
}
