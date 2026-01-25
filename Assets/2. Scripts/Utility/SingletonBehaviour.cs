using UnityEngine;

public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _isQuitting = false;
    private static object _lock = new object();

    public static T I
    {
        get
        {
            if (_isQuitting) return null;

            lock (_lock)
            {
                // 1. 인스턴스가 있는데, 유니티 상에서 파괴된 객체라면 null 처리 (좀비 방지)
                if (_instance != null && _instance as MonoBehaviour == null)
                {
                    _instance = null;
                }

                if (_instance != null)
                    return _instance;

                _instance = FindFirstObjectByType<T>();

                if (_instance != null)
                    return _instance;

                GameObject singletonObject = new GameObject(typeof(T).Name);
                _instance = singletonObject.AddComponent<T>();
                return _instance;
            }
        }
    }

    protected abstract bool IsDontDestroy();

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            if (IsDontDestroy())
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    // 🌟 [핵심 수정] 파괴될 때 static 변수 초기화
    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }
}