using Firebase.Database;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UserRegister : MonoBehaviour
{
    FirebaseDatabase database;
    DatabaseReference reference;
    UnityMainThreadDispatcher dispatcher;

    [Header("Firebase")]
    [SerializeField] string databaseUrl = "https://shingutest-18018-default-rtdb.asia-southeast1.firebasedatabase.app/";

    [Header("UI")]
    [SerializeField] InputField NickNameInput;
    [SerializeField] Text CheckText;

    [Header("Scene")]
    [SerializeField] string NextSceneName = "MainScene";
    [SerializeField] bool LoadNextSceneAfterRegister = false;

    void Start()
    {
        database = FirebaseDatabase.GetInstance(databaseUrl);
        reference = database.RootReference;
        dispatcher = UnityMainThreadDispatcher.Instance();
    }

    // 회원가입 버튼에 연결
    public void OnClickRegister()
    {
        string nickName = NickNameInput.text.Trim();

        if (string.IsNullOrEmpty(nickName))
        {
            CheckText.text = "닉네임을 입력하세요.";
            return;
        }

        CheckDuplicateNickName(nickName);
    }

    void CheckDuplicateNickName(string nickName)
    {
        reference
            .Child("UserInfo")
            .OrderByChild("NickName")
            .EqualTo(nickName)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckText.text = "Firebase 읽기 오류";
                    });
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.HasChildren)
                {
                    dispatcher.Enqueue(() =>
                    {
                        CheckText.text = "이미 사용 중인 닉네임입니다.";
                    });
                    return;
                }

                CreateUser(nickName);
            });
    }

    void CreateUser(string nickName)
    {
        DatabaseReference newUserRef = reference.Child("UserInfo").Push();
        string userKey = newUserRef.Key;

        UserData userData = new UserData(nickName);
        string json = JsonUtility.ToJson(userData);

        newUserRef.SetRawJsonValueAsync(json).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                dispatcher.Enqueue(() =>
                {
                    CheckText.text = "회원가입 실패";
                });
                return;
            }

            dispatcher.Enqueue(() =>
            {
                PlayerPrefs.SetString("UserKey", userKey);
                PlayerPrefs.SetString("UserNickName", nickName);
                PlayerPrefs.Save();

                CheckText.text = "회원가입 완료";

                if (LoadNextSceneAfterRegister)
                {
                    SceneManager.LoadScene(NextSceneName);
                }
            });
        });
    }
}
