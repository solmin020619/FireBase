using Firebase.Database;
using Newtonsoft.Json;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-18018-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] Text PistolCountText;
    [SerializeField] Text ShotgunCountText;
    [SerializeField] Text SniperRifleCountText;
    [SerializeField] Text RocketLauncherCountText;
    [SerializeField] Text MessageText;

    string userKey;
    Dictionary<string, int> inventory = new Dictionary<string, int>();

    readonly Dictionary<string, string> useMessages = new Dictionary<string, string>
    {
        { "Pistol", "피스톨을 사용했습니다. 명중률이 잠시 증가합니다!" },
        { "Shotgun", "샷건을 사용했습니다. 근접 공격력이 증가합니다!" },
        { "SniperRifle", "스나이퍼 라이플을 사용했습니다. 원거리 공격력이 증가합니다!" },
        { "RocketLauncher", "로켓런처를 사용했습니다. 광역 공격력이 증가합니다!" },
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

        LoadInventory();
    }

    void LoadInventory()
    {
        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.Value == null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 데이터가 없습니다.";
                    });
                    return;
                }

                string inventoryJson = snapshot.Value.ToString();
                inventory = JsonConvert.DeserializeObject<Dictionary<string, int>>(inventoryJson);

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = "인벤토리 불러오기 완료";
                });
            });
    }

    void RefreshUI()
    {
        PistolCountText.text = "Pistol : " + GetItemCount("Pistol");
        ShotgunCountText.text = "Shotgun : " + GetItemCount("Shotgun");
        SniperRifleCountText.text = "SniperRifle : " + GetItemCount("SniperRifle");
        RocketLauncherCountText.text = "RocketLauncher : " + GetItemCount("RocketLauncher");
    }

    int GetItemCount(string itemName)
    {
        if (inventory.ContainsKey(itemName))
        {
            return inventory[itemName];
        }

        return 0;
    }

    public void OnClickUsePistol()
    {
        Debug.Log("OnClickUsePistol 호출됨!"); // ← 이 줄 추가
        UseItem("Pistol");
    }

    public void OnClickUseShotgun()
    {
        UseItem("Shotgun");
    }

    public void OnClickUseSniperRifle()
    {
        UseItem("SniperRifle");
    }

    public void OnClickUseRocketLauncher()
    {
        UseItem("RocketLauncher");
    }

    void UseItem(string itemName)
    {
        Debug.Log("UseItem: " + itemName + " / 개수: " + (inventory.ContainsKey(itemName) ? inventory[itemName].ToString() : "키없음")); // ← 이 줄 추가

        if (!inventory.ContainsKey(itemName) || inventory[itemName] <= 0)
        {
            MessageText.text = itemName + " 개수가 부족합니다.";
            return;
        }

        inventory[itemName]--;
        SaveInventory(itemName);
    }

    void SaveInventory(string usedItemName)
    {
        string inventoryJson = JsonConvert.SerializeObject(inventory);

        reference
            .Child("UserInfo")
            .Child(userKey)
            .Child("Inventory")
            .SetValueAsync(inventoryJson)
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        MessageText.text = "인벤토리 저장 실패";
                    });
                    return;
                }

                dispatcher.Enqueue(() =>
                {
                    RefreshUI();
                    MessageText.text = useMessages[usedItemName];
                });
            });
    }
}
