// FigureVisualsConfig.cs
// Хранит ссылки на все спрайты и цвета,
// используемые для визуального представления фигурок.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FigureVisualsConfig", menuName = "Game/Figure Visuals Config")]
public class FigureVisualsConfig : ScriptableObject
{
    // НОВАЯ СТРУКТУРА: для спрайтов, объединяющих форму и цвет.
    [Serializable]
    public struct CombinedShapeColorSpriteEntry
    {
        public ShapeType shapeType;
        public ShapeColorType shapeColorType;
        public Sprite sprite; // Спрайт, который уже содержит и форму, и ее цвет
    }

    [Serializable]
    public struct AnimalSpriteEntry
    {
        public AnimalType type;
        public Sprite sprite;
    }

    [Serializable]
    public struct ShapeColorEntry // Оставлено, если нужно для генерации или UI, но не для отрисовки фишки напрямую
    {
        public ShapeColorType type;
        public Color color;
    }

    [Header("Combined Shape & Color Sprites (4 Shapes x 4 Colors = 16 Sprites)")]
    public List<CombinedShapeColorSpriteEntry> combinedShapeColorSprites;

    [Header("Animal Sprites (4 Animals)")]
    public List<AnimalSpriteEntry> animalSprites;

    [Header("Shape Colors (Optional: for other UI / programmatic use)")]
    public List<ShapeColorEntry> shapeColors; // Это можно удалить, если не используется

    // Вспомогательные методы для получения спрайтов/цветов/данных по типу.

    /// <summary>
    /// Возвращает комбинированный спрайт для заданной формы и цвета.
    /// </summary>
    public Sprite GetCombinedShapeColorSprite(ShapeType shape, ShapeColorType color)
    {
        foreach (var entry in combinedShapeColorSprites)
        {
            if (entry.shapeType == shape && entry.shapeColorType == color)
            {
                return entry.sprite;
            }
        }
        Debug.LogError($"Combined shape-color sprite not found for shape: {shape}, color: {color}");
        return null;
    }

    /// <summary>
    /// Возвращает спрайт животного.
    /// </summary>
    public Sprite GetAnimalSprite(AnimalType type)
    {
        foreach (var entry in animalSprites)
        {
            if (entry.type == type) return entry.sprite;
        }
        Debug.LogError($"Animal sprite not found for type: {type}");
        return null;
    }

    /// <summary>
    /// Возвращает цвет для заданной формы (если еще используется где-то).
    /// </summary>
    public Color GetShapeColor(ShapeColorType type)
    {
        foreach (var entry in shapeColors)
        {
            if (entry.type == type) return entry.color;
        }
        Debug.LogError($"Shape color not found for type: {type}");
        return Color.magenta; // Возвращаем magenta для наглядности ошибки
    }
}