// FigureTypeID.cs
// Структура, которая уникально определяет тип фишки (комбинация формы, цвета и животного).

using UnityEngine; // Для Serializable
using System; // Для IEquatable

[Serializable] // Позволяет сохранять эту структуру в инспекторе Figure
public struct FigureTypeID : IEquatable<FigureTypeID>
{
    public ShapeType Shape;
    public ShapeColorType ShapeColor;
    public AnimalType Animal;

    /// <summary>
    /// Создает новый уникальный ID для фишки.
    /// </summary>
    public FigureTypeID(ShapeType shape, ShapeColorType shapeColor, AnimalType animal)
    {
        Shape = shape;
        ShapeColor = shapeColor;
        Animal = animal;
    }

    /// <summary>
    /// Сравнивает этот ID с другим объектом.
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is FigureTypeID other && Equals(other);
    }

    /// <summary>
    /// Сравнивает этот ID с другим ID фишки.
    /// </summary>
    public bool Equals(FigureTypeID other)
    {
        return Shape == other.Shape &&
               ShapeColor == other.ShapeColor &&
               Animal == other.Animal;
    }

    /// <summary>
    /// Генерирует хеш-код для ID фишки. Нужен для работы в Dictionary и HashSet.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked 
        {
            int hash = 17;
            hash = hash * 23 + Shape.GetHashCode();
            hash = hash * 23 + ShapeColor.GetHashCode();
            hash = hash * 23 + Animal.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// Оператор сравнения на равенство.
    /// </summary>
    public static bool operator ==(FigureTypeID left, FigureTypeID right) => left.Equals(right);
    /// <summary>
    /// Оператор сравнения на неравенство.
    /// </summary>
    public static bool operator !=(FigureTypeID left, FigureTypeID right) => !(left == right);

    /// <summary>
    /// Возвращает строковое представление ID фишки.
    /// </summary>
    public override string ToString()
    {
        return $"{ShapeColor}{Shape}{Animal}";
    }
}