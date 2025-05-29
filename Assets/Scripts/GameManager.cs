// GameManager.cs
// Главный менеджер игры. Управляет состояниями, UI, звуками и другими менеджерами.

using System.Collections;
using UnityEngine;
using UnityEngine.UI; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; } 

    // Состояния игры.
    public enum GameState { StartMenu, Playing, Rerolling, GameOver, Win }
    public GameState CurrentGameState { get; set; } // Текущее состояние игры

    [Header("UI элементы")]
    [SerializeField] private CanvasGroup _startMenuCanvasGroup; // Панель стартового меню
    [SerializeField] private float _fadeDuration = 0.5f;        // Скорость появления/скрытия UI

    [Header("Экраны победы/поражения")]
    [SerializeField] private CanvasGroup _winCanvasGroup;      // Экран победы
    [SerializeField] private CanvasGroup _loseCanvasGroup;     // Экран поражения

    [Header("Игровые менеджеры")]
    [SerializeField] private GameFieldManager _gameFieldManager; // Менеджер игрового поля
    [SerializeField] private ActionBarManager _actionBarManager; // Менеджер панели действий

    [Header("Источники звука")] 
    [SerializeField] private AudioSource _gameOverAudioSource; // Звук при поражении
    [SerializeField] private AudioSource _winAudioSource;      // Звук при победе
    [SerializeField] private AudioSource _clickAudioSource;    // Звук клика
    [SerializeField] private AudioSource _collisionAudioSource; // Звук столкновения

    /// <summary>
    /// Вызывается при загрузке. Настраивает синглтон-инстанс GameManager.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        Debug.Log("[GM] GameManager: Awake.");
    }

    /// <summary>
    /// Вызывается при первом кадре. Запускает начальную настройку игры.
    /// </summary>
    private void Start()
    {
        InitializeGame();
    }

    /// <summary>
    /// Настраивает игру в начальное состояние: скрывает игровые элементы, показывает стартовое меню.
    /// </summary>
    private void InitializeGame()
    {
        CurrentGameState = GameState.StartMenu; 
        Debug.Log($"[GM] Игра в состоянии: {CurrentGameState}.");

        if (_gameFieldManager != null) _gameFieldManager.enabled = false;
        if (_actionBarManager != null) _actionBarManager.enabled = false;
        
        if (_winCanvasGroup != null)
        {
            _winCanvasGroup.alpha = 0;
            _winCanvasGroup.interactable = false;
            _winCanvasGroup.blocksRaycasts = false;
        }
        if (_loseCanvasGroup != null)
        {
            _loseCanvasGroup.alpha = 0;
            _loseCanvasGroup.interactable = false;
            _loseCanvasGroup.blocksRaycasts = false;
        }

        if (_startMenuCanvasGroup != null)
        {
            _startMenuCanvasGroup.alpha = 1;
            _startMenuCanvasGroup.interactable = true;
            _startMenuCanvasGroup.blocksRaycasts = true;
            Debug.Log("[GM] Стартовое меню показано.");
        }
        else
        {
            Debug.LogError("[GM] Стартовое меню не назначено! Автоматический старт игры.", this);
            StartGame(); 
        }

        if (_gameOverAudioSource != null) _gameOverAudioSource.Stop();
        if (_winAudioSource != null) _winAudioSource.Stop();
        if (_clickAudioSource != null) _clickAudioSource.Stop();
        if (_collisionAudioSource != null) _collisionAudioSource.Stop();
    }

    /// <summary>
    /// Запускает игровой процесс после стартового меню.
    /// </summary>
    public void StartGame()
    {
        if (CurrentGameState != GameState.StartMenu) 
        {
            Debug.LogWarning($"[GM] Игнорируем старт игры. Текущее состояние: {CurrentGameState}.");
            return;
        }

        Debug.Log("[GM] Переход от стартового меню к игре.");
        
        if (_startMenuCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(_startMenuCanvasGroup, _startMenuCanvasGroup.alpha, 0, false));
        }

        if (_gameFieldManager != null)
        {
            _gameFieldManager.enabled = true;
            _gameFieldManager.StartFieldGeneration(); 
            Debug.Log("[GM] GameFieldManager активирован.");
        }
        else Debug.LogError("[GM] GameFieldManager не назначен!");

        if (_actionBarManager != null) 
        {
            _actionBarManager.enabled = true;
            Debug.Log("[GM] ActionBarManager активирован.");
        }
        else Debug.LogError("[GM] ActionBarManager не назначен!");
        
        CurrentGameState = GameState.Playing; 
        Debug.Log($"[GM] Игра в состоянии: {CurrentGameState}.");
    }

    /// <summary>
    /// Завершает игру (победа или поражение), показывает соответствующий экран и отключает игровые менеджеры.
    /// </summary>
    /// <param name="won">True, если игрок выиграл, false - если проиграл.</param>
    public void GameOver(bool won)
    {
        if (CurrentGameState == GameState.GameOver || CurrentGameState == GameState.Win) 
        {
            Debug.LogWarning($"[GM] Игра уже завершена ({CurrentGameState}). Игнорируем вызов GameOver.");
            return;
        }

        CurrentGameState = won ? GameState.Win : GameState.GameOver;
        Debug.Log($"[GM] Игра окончена! Результат: {(won ? "Победа" : "Поражение")}. Новое состояние: {CurrentGameState}");

        if (won)
        {
            if (_winAudioSource != null) _winAudioSource.Play();
            if (_winCanvasGroup != null) 
            {
                StartCoroutine(FadeCanvasGroup(_winCanvasGroup, _winCanvasGroup.alpha, 1, true));
                Debug.Log("[GM] Экран победы появляется.");
            } else Debug.LogError("[GM] Экран победы не назначен!");
        }
        else 
        {
            if (_gameOverAudioSource != null) _gameOverAudioSource.Play();
            if (_loseCanvasGroup != null) 
            {
                StartCoroutine(FadeCanvasGroup(_loseCanvasGroup, _loseCanvasGroup.alpha, 1, true));
                Debug.Log("[GM] Экран поражения появляется.");
            } else Debug.LogError("[GM] Экран поражения не назначен!");
        }

        if (_gameFieldManager != null) _gameFieldManager.enabled = false;
        else Debug.LogWarning("[GM] GameFieldManager отсутствует, не могу отключить.");

        if (_actionBarManager != null) _actionBarManager.enabled = false;
        else Debug.LogWarning("[GM] ActionBarManager отсутствует, не могу отключить.");
    }

    /// <summary>
    /// Перезапускает игру, скрывая экраны победы/поражения и запуская обновление уровня.
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GM] Перезапуск игры начат.");
        
        if (_winCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(_winCanvasGroup, _winCanvasGroup.alpha, 0, false));
        }
        if (_loseCanvasGroup != null)
        {
            StartCoroutine(FadeCanvasGroup(_loseCanvasGroup, _loseCanvasGroup.alpha, 0, false));
        }

        CurrentGameState = GameState.Rerolling; 
        Debug.Log($"[GM] Состояние игры: {CurrentGameState}.");

        if (_gameFieldManager != null) _gameFieldManager.enabled = true;
        else Debug.LogWarning("[GM] GameFieldManager отсутствует, не могу включить.");

        if (_actionBarManager != null) _actionBarManager.enabled = true;
        else Debug.LogWarning("[GM] ActionBarManager отсутствует, не могу включить.");

        if (_gameFieldManager != null)
        {
            _gameFieldManager.RerollLevel();
            Debug.Log("[GM] GameFieldManager.RerollLevel вызван.");
        }
        else
        {
            Debug.LogError("[GM] GameFieldManager не назначен для перезапуска!", this);
        }
    }

    /// <summary>
    /// Воспроизводит звук клика.
    /// </summary>
    public void PlayClickSound()
    {
        if (_clickAudioSource != null)
        {
            _clickAudioSource.PlayOneShot(_clickAudioSource.clip);
        }
        else
        {
            Debug.LogWarning("[GM] Нет AudioSource для клика.");
        }
    }

    /// <summary>
    /// Воспроизводит звук столкновения.
    /// </summary>
    public void PlayCollisionSound()
    {
        if (_collisionAudioSource != null)
        {
            _collisionAudioSource.PlayOneShot(_collisionAudioSource.clip);
        }
        else
        {
            Debug.LogWarning("[GM] Нет AudioSource для столкновений.");
        }
    }

    /// <summary>
    /// Плавно меняет прозрачность CanvasGroup (для UI элементов).
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, bool interactableAtEnd)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("[GM] CanvasGroup не назначен для затемнения.");
            yield break;
        }
        Debug.Log($"[GM] Затемнение '{canvasGroup.gameObject.name}' с {startAlpha} до {endAlpha}. Интерактивность: {interactableAtEnd}.");
        
        float timer = 0f;
        float currentAlpha = canvasGroup.alpha;

        if (endAlpha < currentAlpha) 
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        while (timer < _fadeDuration)
        {
            timer += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(currentAlpha, endAlpha, timer / _fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha; 
        canvasGroup.interactable = interactableAtEnd;
        canvasGroup.blocksRaycasts = interactableAtEnd;
    }
}