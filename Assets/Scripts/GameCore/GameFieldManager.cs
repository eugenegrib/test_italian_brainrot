// GameFieldManager.cs
// Управляет игровым полем: создает фишки, следит за ними и перезапускает уровень.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI; 

public class GameFieldManager : MonoBehaviour
{
    [Header("Настройки создания фишек")]
    [SerializeField] [Tooltip("Сколько фишек будет на поле в начале игры.")]
    private int _initialFigureCount = 60; 
    [SerializeField] [Tooltip("Насколько высоко над GameFieldManager будут спавниться фишки по оси Y.")]
    private float _spawnOffset = 15f; 
    [SerializeField] [Tooltip("Задержка между спавном каждой фишки для эффекта 'песка'.")]
    private float _spawnDelay = 0.05f; 
    [SerializeField] [Tooltip("Максимальный случайный разброс по оси X при спавне фишек.")]
    private float _horizontalSpawnStagger = 0.5f; 
    [SerializeField] [Tooltip("Максимальный случайный разброс по оси Y при спавне фишек.")]
    private float _verticalSpawnStagger = 0.5f; 
    [SerializeField] [Tooltip("Радиус проверки свободного места при спавне фишек. Должен быть примерно равен радиусу фишки.")]
    private float _spawnCheckRadius = 0.5f; 
    [SerializeField] [Tooltip("Слой, на котором находятся игровые фишки. Используется для проверки перекрытий.")]
    private LayerMask _figureLayer; 
    [SerializeField] [Tooltip("Максимальное количество попыток найти свободное место для спавна каждой фишки.")]
    private int _maxSpawnAttemptsPerFigure = 10; 

    private List<Figure> _figuresOnField = new List<Figure>(); // Все фишки, что сейчас на поле

    [Header("Границы поля")]
    [SerializeField] [Tooltip("Ссылка на коллайдер пола игрового поля. Используется для эффекта 'люка' при обновлении.")]
    private Collider2D _fieldFloorCollider; 

    [Header("Обновление уровня и время")]
    [SerializeField] [Tooltip("Длительность падения фишек 'сквозь' пол при обновлении уровня.")]
    private float _trapdoorFallDuration = 3.0f; 
    [SerializeField] [Tooltip("Пауза после очистки поля перед созданием новых фишек.")]
    private float _delayBeforeNewSpawn = 0.5f; 
    [SerializeField] [Tooltip("Время, чтобы фишки полностью осели после первого спавна.")]
    private float _initialSettlingTime = 2.0f; 
    [SerializeField] [Tooltip("Время, чтобы фишки полностью осели после обновления уровня.")]
    private float _rerollSettlingTime = 2.0f; 

    [Header("UI элементы")]
    [SerializeField] [Tooltip("CanvasGroup кнопки 'Обновить уровень'.")]
    private CanvasGroup _rerollButtonCanvasGroup; 
    [SerializeField] [Tooltip("Длительность анимации появления/скрытия UI элементов.")]
    private float _fadeDuration = 0.5f; 

    private bool _isRerrolling = false; // Флаг: идет ли обновление уровня

    /// <summary>
    /// Настраивает менеджер при запуске. Подписывается на события и скрывает кнопку обновления.
    /// </summary>
    void Start()
    {
        Figure.OnFigureClicked += HandleFigureClicked;

        if (_rerollButtonCanvasGroup != null)
        {
            _rerollButtonCanvasGroup.alpha = 0;
            _rerollButtonCanvasGroup.interactable = false;
            _rerollButtonCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Обрабатывает клик по фишке: убирает её с поля, проверяет победу и отправляет в бар.
    /// </summary>
    /// <param name="clickedFigure">Фишка, по которой кликнули.</param>
    private void HandleFigureClicked(Figure clickedFigure)
    {
        if (clickedFigure == null) return;

        GameManager.Instance?.PlayClickSound();

        if (_figuresOnField.Contains(clickedFigure))
        {
            _figuresOnField.Remove(clickedFigure); 

            if (_figuresOnField.Count == 0)
            {
                Debug.Log("[Поле] Все фишки убраны. Выигрыш.");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.GameOver(true); 
                }
                else
                {
                    Debug.LogError("[Поле] Нет GameManager для вызова победы!");
                }
                return; 
            }
        }
        else
        {
            Debug.LogWarning($"[Поле] Кликнутая фишка {clickedFigure.name} не найдена в списке.", clickedFigure);
            return; 
        }
        
        if (ActionBarManager.Instance != null)
        {
            ActionBarManager.Instance.AddFigureToBar(clickedFigure);
        }
        else
        {
            Debug.LogError("[Поле] Нет менеджера бара! Фишка вернется в пул.", this);
            ObjectPoolManager.Instance?.ReturnFigure(clickedFigure);
        }
    }

    /// <summary>
    /// Отписывается от событий при уничтожении объекта.
    /// </summary>
    private void OnDestroy()
    {
        Figure.OnFigureClicked -= HandleFigureClicked;
    }

    /// <summary>
    /// Запускает начальную генерацию игрового поля.
    /// </summary>
    public void StartFieldGeneration() 
    {
        if (ObjectPoolManager.Instance == null || ObjectPoolManager.Instance.AvailableFigureTypes.Count == 0)
        {
            Debug.LogError("GameFieldManager: Нет пула или типов фишек!");
            return;
        }

        StartCoroutine(InitialFieldGenerationAndButtonShow(_initialFigureCount));
    }

    /// <summary>
    /// Генерирует поле и показывает кнопку обновления после оседания фишек.
    /// </summary>
    /// <param name="countToSpawn">Сколько фишек нужно создать.</param>
    private IEnumerator InitialFieldGenerationAndButtonShow(int countToSpawn)
    {
        yield return StartCoroutine(GenerateFieldCoroutine(countToSpawn));

        yield return new WaitForSeconds(_initialSettlingTime);

        if (_rerollButtonCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(_rerollButtonCanvasGroup, _rerollButtonCanvasGroup.alpha, 1, true));
        }
        Debug.Log("[Поле] Поле создано, кнопка 'обновить' показана.");
    }

    /// <summary>
    /// Запускает процесс обновления игрового поля.
    /// </summary>
    public void RerollLevel()
    {
        if (_isRerrolling)
        {
            Debug.Log("[Поле] Обновление уже идет.");
            return;
        }
        _isRerrolling = true; 
        Debug.Log("[Поле] Начинаем обновлять уровень...");

        // Устанавливаем состояние Rerolling, пока идет процесс.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentGameState = GameManager.GameState.Rerolling;
        }

        StartCoroutine(RerollLevelCoroutine()); 
    }

    /// <summary>
    /// Выполняет полный цикл обновления поля: очистку, сброс бара и новую генерацию.
    /// </summary>
    private IEnumerator RerollLevelCoroutine()
    {
        if (_rerollButtonCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(_rerollButtonCanvasGroup, _rerollButtonCanvasGroup.alpha, 0, false));
        }

        if (ActionBarManager.Instance != null)
        {
            yield return StartCoroutine(ActionBarManager.Instance.ClearActionBarAnimated());
            ActionBarManager.Instance.ResetBar(); 
        }
        
        if (_fieldFloorCollider != null)
        {
            _fieldFloorCollider.enabled = false; 
            Debug.Log("[Поле] Коллайдер пола ВЫКЛ.");
        }
        else
        {
            Debug.LogWarning("[Поле] Нет коллайдера пола!");
        }

        yield return new WaitForSeconds(_trapdoorFallDuration);

        ObjectPoolManager.Instance?.ReturnAllActiveFiguresToPool(_figuresOnField);
        Debug.Log("[Поле] Все фишки вернулись в пул.");

        if (_fieldFloorCollider != null)
        {
            _fieldFloorCollider.enabled = true; 
            Debug.Log("[Поле] Коллайдер пола ВКЛ.");
        }

        yield return new WaitForSeconds(_delayBeforeNewSpawn);

        yield return StartCoroutine(GenerateFieldCoroutine(_initialFigureCount));

        yield return new WaitForSeconds(_rerollSettlingTime);

        if (_rerollButtonCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(_rerollButtonCanvasGroup, _rerollButtonCanvasGroup.alpha, 1, true));
        }

        _isRerrolling = false; 
        // После завершения реролла, устанавливаем состояние игры обратно в Playing.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CurrentGameState = GameManager.GameState.Playing;
            Debug.Log($"[Поле] Состояние игры: {GameManager.Instance.CurrentGameState}");
        }
        Debug.Log("[Поле] Уровень обновлен!");
    }

    /// <summary>
    /// Создает и размещает новый набор фишек на поле, избегая сильных наложений.
    /// </summary>
    /// <param name="countToSpawn">Сколько фишек нужно создать.</param>
 private IEnumerator GenerateFieldCoroutine(int countToSpawn)
    {
        _figuresOnField.Clear(); 

        // 1. Создаем временный словарь, отражающий текущее количество фишек в пуле для каждого типа.
        // Это нужно, чтобы "виртуально" брать фишки из пула при планировании генерации.
        Dictionary<FigureTypeID, int> availableCountsForGeneration = new Dictionary<FigureTypeID, int>();
        if (ObjectPoolManager.Instance == null || ObjectPoolManager.Instance.AvailableFigureTypes.Count == 0)
        {
            Debug.LogError("GameFieldManager: Нет пула или типов фишек!");
            yield break;
        }

        foreach (FigureTypeID id in ObjectPoolManager.Instance.AvailableFigureTypes)
        {
            availableCountsForGeneration[id] = ObjectPoolManager.Instance.GetAvailableCount(id);
        }

        List<FigureTypeID> figureIDsToSpawn = new List<FigureTypeID>();
        List<FigureTypeID> viableTypesInThisLoop = new List<FigureTypeID>(); // Список типов, из которых можно взять 3 фишки

        // 2. Генерируем ID всех фишек, которые нужно будет заспавнить, гарантируя кратность 3.
        int groupsToSpawn = countToSpawn / 3; // Сколько групп по 3 фишки нужно создать
        for (int i = 0; i < groupsToSpawn; i++)
        {
            viableTypesInThisLoop.Clear();
            // Находим все типы фишек, для которых сейчас есть минимум 3 фишки в нашем "виртуальном" пуле.
            foreach (var entry in availableCountsForGeneration)
            {
                if (entry.Value >= 3)
                {
                    viableTypesInThisLoop.Add(entry.Key);
                }
            }

            if (viableTypesInThisLoop.Count == 0)
            {
                Debug.LogWarning("[Поле] Невозможно сгенерировать больше групп по 3 фишки: нет типов с достаточным количеством в пуле.");
                break; // Выходим из цикла, если больше нет доступных групп.
            }

            // Выбираем случайный тип из тех, что доступны в группах по 3.
            FigureTypeID randomFigureID = viableTypesInThisLoop[UnityEngine.Random.Range(0, viableTypesInThisLoop.Count)];

            // Добавляем 3 фишки этого типа в список для спавна.
            for (int j = 0; j < 3; j++)
            {
                figureIDsToSpawn.Add(randomFigureID);
            }
            // "Забираем" эти 3 фишки из нашего временного пула, чтобы их не выбрали снова.
            availableCountsForGeneration[randomFigureID] -= 3;
        }

        ShuffleList(figureIDsToSpawn); // Перемешиваем список, чтобы порядок падения был случайным.

        // 3. Далее, обычная логика спавна по списку figureIDsToSpawn.
        // Теперь мы гарантируем, что GetFigure() будет успешен для всех фишек в этом списке.
        float baseSpawnY = transform.position.y + _spawnOffset; 

        foreach (FigureTypeID id in figureIDsToSpawn)
        {
            Vector3 spawnPosition = Vector3.zero;
            bool foundValidSpawnPosition = false;
            int attempts = 0;

            // Ищем свободное место для спавна, чтобы фишки не накладывались сильно.
            while (!foundValidSpawnPosition && attempts < _maxSpawnAttemptsPerFigure)
            {
                float staggeredX = UnityEngine.Random.Range(-4f, 4f) + UnityEngine.Random.Range(-_horizontalSpawnStagger, _horizontalSpawnStagger);
                float staggeredY = baseSpawnY + UnityEngine.Random.Range(-_verticalSpawnStagger, _verticalSpawnStagger);
                spawnPosition = new Vector3(staggeredX, staggeredY, 0);

                // Проверяем, есть ли другие фишки в этой точке.
                Collider2D[] hitColliders = Physics2D.OverlapCircleAll(spawnPosition, _spawnCheckRadius, _figureLayer);
                
                if (hitColliders.Length == 0) 
                {
                    foundValidSpawnPosition = true;
                }
                attempts++;
            }

            if (!foundValidSpawnPosition)
            {
                Debug.LogWarning($"[Поле] Не нашли свободное место для фишки {id} после {attempts} попыток. Могут быть наложения.");
            }

            Figure newFigure = ObjectPoolManager.Instance?.GetFigure(id);

            if (newFigure != null)
            {
                newFigure.transform.position = spawnPosition;
                newFigure.transform.rotation = Quaternion.identity;
                newFigure.transform.SetParent(this.transform); 

                newFigure.Initialize(); 
                _figuresOnField.Add(newFigure); 
            }
            else
            {
                // Эта ошибка теперь не должна происходить, если логика выше отработала правильно.
                Debug.LogError($"[Поле] Ошибка! Не взяли фишку {id} из пула, хотя должны были. Пул пуст?", this);
            }

            yield return new WaitForSeconds(_spawnDelay); 
        }

        Debug.Log("[Поле] Поле создано!");
    }
    /// <summary>
    /// Перемешивает список элементов.
    /// </summary>
    /// <typeparam name="T">Тип элементов списка.</typeparam>
    /// <param name="list">Список для перемешивания.</param>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    /// <summary>
    /// Плавно меняет прозрачность CanvasGroup (для UI элементов).
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, bool interactableAtEnd)
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("[Поле] Нет CanvasGroup для затемнения.");
            yield break;
        }

        float timer = 0f;
        float currentAlpha = canvasGroup.alpha; 

        if (endAlpha < currentAlpha) // Если прячем, сразу выключаем клики.
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