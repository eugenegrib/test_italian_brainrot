// PoolPreCreator.cs
// Инструмент для редактора Unity. Помогает заранее создать и настроить все нужные фишки для пула.

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;

public class PoolPreCreator : EditorWindow
{
    private GameObject _baseFigurePrefab; // Основной префаб фишки (с компонентами Figure и физики)
    private FigureVisualsConfig _visualsConfig; // Конфиг с визуалами фишек
    private ObjectPoolManager _poolManager; // Ссылка на менеджер пула в сцене
    private string _poolParentObjectName = "FigurePool"; // Название родительского объекта для фишек в пуле
    
    // ИЗМЕНЕНО: Название поля и его смысл
    [Tooltip("Сколько полных групп по 3 экземпляра каждого уникального типа фишки создать в пуле.")]
    private int _numberOfTriplesPerType = 10; // Например, 10 троек каждого типа

    /// <summary>
    /// Добавляет пункт "Populate Unique Figure Pool" в меню "Tools/Game".
    /// </summary>
    [MenuItem("Tools/Game/Populate Unique Figure Pool")]
    public static void ShowWindow()
    {
        GetWindow<PoolPreCreator>("Populate Unique Figure Pool");
    }

    /// <summary>
    /// Рисует интерфейс окна редактора.
    /// </summary>
    private void OnGUI()
    {
        GUILayout.Label("Создать фишки для пула (2 слоя)", EditorStyles.boldLabel);

        _baseFigurePrefab = (GameObject)EditorGUILayout.ObjectField("Базовый префаб фишки", _baseFigurePrefab, typeof(GameObject), false);
        _visualsConfig = (FigureVisualsConfig)EditorGUILayout.ObjectField("Конфиг визуалов", _visualsConfig, typeof(FigureVisualsConfig), false);
        _poolManager = (ObjectPoolManager)EditorGUILayout.ObjectField("Менеджер пула (на сцене)", _poolManager, typeof(ObjectPoolManager), true);
        
        // ИЗМЕНЕНО: Используем новое название поля
        _numberOfTriplesPerType = EditorGUILayout.IntField("Кол-во троек каждого типа в пуле", _numberOfTriplesPerType);
        _numberOfTriplesPerType = Mathf.Max(1, _numberOfTriplesPerType); // Минимум 1 тройка

        if (GUILayout.Button("Создать пул (очистить и создать)"))
        {
            PopulatePool();
        }

        if (_baseFigurePrefab == null || _visualsConfig == null || _poolManager == null)
        {
            EditorGUILayout.HelpBox("Назначьте базовый префаб, конфиг визуалов и менеджер пула.", MessageType.Warning);
        }
    }

    /// <summary>
    /// Очищает существующий пул и создает новые экземпляры фишек для каждого уникального типа.
    /// </summary>
    private void PopulatePool()
    {
        if (_baseFigurePrefab == null || _visualsConfig == null || _poolManager == null)
        {
            Debug.LogError("Не могу создать пул: не назначены все нужные объекты.");
            return;
        }
        if (_baseFigurePrefab.GetComponent<Figure>() == null)
        {
            Debug.LogError("Базовый префаб фишки должен иметь скрипт 'Figure' на корневом объекте.", _baseFigurePrefab);
            return;
        }

        ClearExistingPoolObjects(); // Сначала чистим все, что было

        Transform poolParent = _poolManager.transform.Find(_poolParentObjectName);
        if (poolParent == null)
        {
            poolParent = new GameObject(_poolParentObjectName).transform;
            poolParent.SetParent(_poolManager.transform);
        }

        int totalCreatedCount = 0;
        AssetDatabase.StartAssetEditing(); // Начинаем быструю обработку ассетов

        ShapeType[] shapeTypes = (ShapeType[])Enum.GetValues(typeof(ShapeType));
        ShapeColorType[] shapeColorTypes = (ShapeColorType[])Enum.GetValues(typeof(ShapeColorType));
        AnimalType[] animalTypes = (AnimalType[])Enum.GetValues(typeof(AnimalType));

        foreach (ShapeType shape in shapeTypes)
        {
            foreach (ShapeColorType shapeColor in shapeColorTypes)
            {
                // Пропускаем, если для этой КОМБИНАЦИИ формы и цвета нет готового спрайта
                if (_visualsConfig.GetCombinedShapeColorSprite(shape, shapeColor) == null)
                {
                    Debug.LogWarning($"Пропускаем комбинацию {shape}-{shapeColor}: нет комбинированного спрайта в конфиге.", _visualsConfig);
                    continue;
                }

                foreach (AnimalType animal in animalTypes)
                {
                    // Пропускаем, если нет спрайта животного.
                    if (_visualsConfig.GetAnimalSprite(animal) == null)
                    {
                        Debug.LogWarning($"Пропускаем животное {animal}: нет спрайта животного в конфиге.", _visualsConfig);
                        continue;
                    }

                    // ИЗМЕНЕНО: Создаем N * 3 экземпляров каждого типа
                    for (int i = 0; i < _numberOfTriplesPerType * 3; i++)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(_baseFigurePrefab, poolParent);

                        Figure figureComponent = instance.GetComponent<Figure>();

                        // !!!! ВАЖНО: УБЕДИТЕСЬ, ЧТО СТРУКТУРА ВАШЕГО _baseFigurePrefab ТЕПЕРЬ СОДЕРЖИТ
                        // GameObject для CombinedShapeColorRenderer и AnimalRenderer, и они
                        // НАЗНАЧЕНЫ В Figure.cs В ИНСПЕКТОРЕ.

                        // Назначаем ID фишке.
                        FigureTypeID currentFigureID = new FigureTypeID(shape, shapeColor, animal);
                        SerializedObject so = new SerializedObject(figureComponent);
                        SerializedProperty figureIDProp = so.FindProperty("_figureID");
                        figureIDProp.FindPropertyRelative("Shape").enumValueIndex = (int)currentFigureID.Shape;
                        figureIDProp.FindPropertyRelative("ShapeColor").enumValueIndex = (int)currentFigureID.ShapeColor;
                        figureIDProp.FindPropertyRelative("Animal").enumValueIndex = (int)currentFigureID.Animal;
                        so.ApplyModifiedProperties();

                        // Настраиваем визуал и коллайдер фишки.
                        // Этот метод теперь использует только два рендерера!
                        figureComponent.SetVisualsAndColliderInEditor(currentFigureID, _visualsConfig);
                        EditorUtility.SetDirty(figureComponent);

                        instance.SetActive(false); // Деактивируем фишку
                        _poolManager.AddPreCreatedFigure(figureComponent); // Добавляем в список менеджера пула

                        totalCreatedCount++;
                    }
                }
            }
        }

        AssetDatabase.StopAssetEditing(); // Заканчиваем быструю обработку
        EditorUtility.SetDirty(_poolManager);
        AssetDatabase.SaveAssets(); // Сохраняем ассеты

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); // Помечаем сцену как измененную
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); // Сохраняем сцену
        AssetDatabase.Refresh(); // Обновляем базу ассетов

        Debug.Log($"Создано {totalCreatedCount} уникальных фишек и назначено менеджеру пула. НЕ ЗАБУДЬТЕ СОХРАНИТЬ СЦЕНУ!");
        EditorUtility.DisplayDialog("Пул создан", $"Создано {totalCreatedCount} уникальных фишек. Не забудьте сохранить сцену!", "ОК");
    }

    /// <summary>
    /// Очищает все существующие объекты пула на сцене.
    /// </summary>
    private void ClearExistingPoolObjects()
    {
        if (_poolManager == null) return;

        Transform poolParent = _poolManager.transform.Find(_poolParentObjectName);
        if (poolParent != null)
        {
            List<GameObject> childrenToDestroy = new List<GameObject>();
            foreach (Transform child in poolParent)
            {
                childrenToDestroy.Add(child.gameObject);
            }
            foreach (GameObject go in childrenToDestroy)
            {
                DestroyImmediate(go); // Удаляем объекты сразу
            }
            DestroyImmediate(poolParent.gameObject); // Удаляем родительский объект пула
        }
        _poolManager.ClearPreCreatedFiguresList(); // Очищаем список в менеджере пула
        EditorUtility.SetDirty(_poolManager);
        AssetDatabase.SaveAssets();
    }
}