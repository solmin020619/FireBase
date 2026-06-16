using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 심화 2. 게임 결과 저장
public class GameResultManager : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-18018-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] InputField ScoreInput;
    [SerializeField] InputField RewardCoinInput;
    [SerializeField] Text ResultText;

    string userKey;

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();

        userKey = PlayerPrefs.GetString("UserKey");

        if (string.IsNullOrEmpty(userKey))
        {
            ResultText.text = "로그인 정보가 없습니다.";
            return;
        }
    }

    // 게임 종료 버튼에 연결
    public void OnClickEndGame()
    {
        int.TryParse(ScoreInput.text, out int earnedScore);
        int.TryParse(RewardCoinInput.text, out int rewardCoin);

        EndGame(earnedScore, rewardCoin);
    }

    void EndGame(int earnedScore, int rewardCoin)
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
                        ResultText.text = "유저 정보 불러오기 실패";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                int currentCoin = int.Parse(snapshot.Child("Coin").Value.ToString());
                int currentScore = int.Parse(snapshot.Child("Score").Value.ToString());

                int newCoin = currentCoin + rewardCoin;
                int newScore = Mathf.Max(currentScore, earnedScore);

                Dictionary<string, object> updateData = new Dictionary<string, object>();
                updateData["Coin"] = newCoin;
                updateData["Score"] = newScore;

                reference
                    .Child("UserInfo")
                    .Child(userKey)
                    .UpdateChildrenAsync(updateData)
                    .ContinueWith(saveTask =>
                    {
                        if (saveTask.IsFaulted)
                        {
                            dispatcher.Enqueue(() =>
                            {
                                ResultText.text = "게임 결과 저장 실패";
                            });
                            return;
                        }

                        dispatcher.Enqueue(() =>
                        {
                            bool isNewRecord = newScore > currentScore;
                            ResultText.text = "보상 " + rewardCoin + " 코인 획득! 최고 점수 : " + newScore
                                + (isNewRecord ? " (신기록!)" : "");
                        });
                    });
            });
    }
}
