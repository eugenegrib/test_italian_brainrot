using UnityEngine;

public class TestClickScript : MonoBehaviour
{
    void OnMouseDown()
    {
        Debug.Log($"Тестовый клик на {gameObject.name}!");
    }
}