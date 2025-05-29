// ObjectPoolManager.cs
// Управляет пулом игровых объектов Figure.

using System.Collections.Generic;
using UnityEngine;
using System;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [SerializeField] private List<Figure> _preCreatedFigures = new List<Figure>();

    private Dictionary<FigureTypeID, Queue<Figure>> _figurePools = new Dictionary<FigureTypeID, Queue<Figure>>();

    public HashSet<FigureTypeID> AvailableFigureTypes { get; private set; } = new HashSet<FigureTypeID>();

    private Transform _poolParent;

    /// <summary>
    /// Настраивает менеджер пула при старте. Создает родительский объект для пула и инициализирует его.
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

        _poolParent = this.transform.Find("FigurePool");
        if (_poolParent == null)
        {
            _poolParent = new GameObject("FigurePool").transform;
            _poolParent.SetParent(this.transform);
        }

        InitializePoolFromPreCreated();
    }

    /// <summary>
    /// Инициализирует пул из предварительно созданных фигур (настроенных в Editor).
    /// </summary>
    private void InitializePoolFromPreCreated()
    {
        if (_preCreatedFigures.Count == 0)
        {
            Debug.LogWarning("[Пул] Нет предварительно созданных фишек. Запустите 'Tools/Game/Populate Unique Figure Pool' и сохраните сцену!");
            return;
        }

        _figurePools.Clear();
        AvailableFigureTypes.Clear();

        foreach (Figure figure in _preCreatedFigures)
        {
            if (figure != null)
            {
                FigureTypeID figureID = figure.FigureID;
                
                // Проверяем ID фишки на корректность.
                if (figureID.Shape == default(ShapeType) && figureID.ShapeColor == default(ShapeColorType) && figureID.Animal == default(AnimalType) && !figure.name.Contains("(Clone)"))
                {
                     Debug.LogWarning($"[Пул] Фишка '{figure.name}' имеет неверный ID ({figureID}). Уничтожена. Пересоздайте пул.", figure);
                     Destroy(figure.gameObject);
                     continue;
                }

                if (!_figurePools.ContainsKey(figureID))
                {
                    _figurePools[figureID] = new Queue<Figure>();
                    AvailableFigureTypes.Add(figureID);
                }
                figure.gameObject.SetActive(false); // Деактивируем фишку
                figure.transform.SetParent(_poolParent); // Перемещаем в родительский объект пула
                _figurePools[figureID].Enqueue(figure); // Добавляем в очередь
            }
            else
            {
                Debug.LogWarning("[Пул] Список предсозданных фишек содержит пустую запись.");
            }
        }
        Debug.Log($"[Пул] Инициализировано {AvailableFigureTypes.Count} уникальных типов фишек. Всего фишек: {_preCreatedFigures.Count}.");
        _preCreatedFigures.Clear();
    }

    /// <summary>
    /// Выдает фишку нужного типа из пула.
    /// </summary>
    /// <param name="desiredID">ID нужной фишки.</param>
    /// <returns>Готовая к использованию фишка или null, если пул пуст для этого типа.</returns>
    public Figure GetFigure(FigureTypeID desiredID)
    {
        if (!_figurePools.ContainsKey(desiredID) || _figurePools[desiredID].Count == 0)
        {
            Debug.LogError($"[Пул] Пул для '{desiredID.ToString()}' пуст! Увеличьте размер пула через Editor Tool.");
            return null;
        }

        Figure figure = _figurePools[desiredID].Dequeue();
        figure.gameObject.SetActive(true); // Активируем фишку
        figure.ResetPhysicsState(); // Сбрасываем физику для новой жизни
        return figure;
    }

    /// <summary>
    /// Возвращает фишку обратно в пул.
    /// </summary>
    /// <param name="figure">Фишка для возврата.</param>
    public void ReturnFigure(Figure figure)
    {
        if (figure == null)
        {
            Debug.LogWarning("[Пул] Попытка вернуть пустую фишку в пул.");
            return;
        }

        FigureTypeID figureID = figure.FigureID;
        // Проверяем ID фишки на валидность при возврате.
        if (figureID.Shape == 0 && figureID.ShapeColor == 0 && figureID.Animal == 0 && figure.name != "Figure (Clone)")
        {
            Debug.LogWarning($"[Пул] Попытка вернуть фишку '{figure.name}' с неверным ID ({figureID}). Уничтожена.", figure);
            Destroy(figure.gameObject);
            return;
        }

        if (!_figurePools.ContainsKey(figureID))
        {
            Debug.LogError($"[Пул] Нет пула для '{figureID.ToString()}'. Фишка будет уничтожена.", figure);
            Destroy(figure.gameObject);
            return;
        }

        figure.gameObject.SetActive(false); // Деактивируем фишку
        figure.transform.SetParent(_poolParent); // Перемещаем в родительский объект пула
        _figurePools[figureID].Enqueue(figure); // Добавляем в очередь
    }

    /// <summary>
    /// Возвращает все активные фишки из заданного списка обратно в пул.
    /// </summary>
    /// <param name="figures">Список активных фишек.</param>
    public void ReturnAllActiveFiguresToPool(List<Figure> figures)
    {
        List<Figure> figuresToReturn = new List<Figure>(figures); // Делаем копию, чтобы избежать ошибок при изменении списка во время итерации
        foreach (Figure figure in figuresToReturn)
        {
            if (figure != null && figure.gameObject.activeInHierarchy)
            {
                ReturnFigure(figure);
            }
        }
        figures.Clear(); // Очищаем оригинальный список
    }
    
    /// <summary>
    /// Возвращает количество доступных фишек конкретного типа в пуле.
    /// </summary>
    /// <param name="id">ID типа фишки.</param>
    /// <returns>Количество доступных фишек этого типа.</returns>
    public int GetAvailableCount(FigureTypeID id)
    {
        if (_figurePools.ContainsKey(id))
        {
            return _figurePools[id].Count;
        }
        return 0; // Если такого пула нет, значит, доступно 0 фишек.
    }
    
    /// <summary>
    /// Добавляет фишку в список предварительно созданных (используется Editor Tool).
    /// </summary>
    /// <param name="figure">Фишка для добавления.</param>
    public void AddPreCreatedFigure(Figure figure)
    {
        if (!_preCreatedFigures.Contains(figure))
        {
            _preCreatedFigures.Add(figure);
        }
    }

    /// <summary>
    /// Очищает список предварительно созданных фишек (используется Editor Tool).
    /// </summary>
    public void ClearPreCreatedFiguresList()
    {
        _preCreatedFigures.Clear();
    }
}