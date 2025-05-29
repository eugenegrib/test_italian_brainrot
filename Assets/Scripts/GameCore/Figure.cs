// Figure.cs
// Скрипт, управляющий поведением отдельной фишки на игровом поле.
// Отвечает за визуал, физику, обработку кликов и воспроизведение звука первого касания.

using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Figure : MonoBehaviour
{
    // Уникальный идентификатор этой фишки.
    [SerializeField] private FigureTypeID _figureID;
    public FigureTypeID FigureID => _figureID;

    // Компоненты физики, прикрепленные к корневому GameObject.
    private PolygonCollider2D _collider2D;
    private Rigidbody2D _rigidbody2D;

    [Header("Визуальные компоненты (назначить в редакторе)")]
    // НОВЫЕ РЕНДЕРЕРЫ: один для формы+цвета, другой для животного.
    [SerializeField] private SpriteRenderer _combinedShapeColorRenderer; // Для спрайта, объединяющего форму и цвет
    [SerializeField] private SpriteRenderer _animalRenderer; // Для спрайта животного

    [Header("Настройки перетаскивания/клика")]
    [SerializeField] private float _dragForce = 100f;
    [SerializeField] private float _dragThreshold = 0.5f;

    private Vector3 _mouseDownWorldPos;
    private bool _isDragging = false;

    private bool _hasPlayedCollisionSound = false; // Флаг: был ли уже звук столкновения
    [SerializeField] [Tooltip("Задержка в секундах, после которой фишка может начать издавать звук столкновения.")]
    private float _collisionSoundDelayAfterSpawn = 1.0f; // Задержка для звука столкновения
    private float _collisionSoundActivationTime; // Время, когда звук столкновения станет активен

    // Событие, которое срабатывает при "чистом" клике на фишку (без перетаскивания).
    public static event Action<Figure> OnFigureClicked;

    void Awake()
    {
        _collider2D = GetComponent<PolygonCollider2D>();
        _rigidbody2D = GetComponent<Rigidbody2D>();

        if (_collider2D == null) Debug.LogError("Figure: PolygonCollider2D не найден на корневом объекте!", this);
        if (_rigidbody2D == null) Debug.LogError("Figure: Rigidbody2D не найден на корневом объекте!", this);

        // Обновленные проверки для рендереров
        if (_combinedShapeColorRenderer == null) Debug.LogWarning("Figure: Combined Shape Color Renderer не назначен!", this);
        if (_animalRenderer == null) Debug.LogWarning("Figure: Animal Renderer не назначен!", this);
    }

    /// <summary>
    /// Вызывается после создания/получения из пула для сброса состояния фишки.
    /// </summary>
    public void Initialize()
    {
        ResetPhysicsState();
        _hasPlayedCollisionSound = false;
        _collisionSoundActivationTime = Time.time + _collisionSoundDelayAfterSpawn; // Задаем время активации звука
    }

    /// <summary>
    /// Этот метод вызывается ИСКЛЮЧИТЕЛЬНО в EDITOR-скрипте PoolPreCreator
    /// для начальной настройки визуалов и коллайдера префаба/инстанса в сцене.
    /// </summary>
    public void SetVisualsAndColliderInEditor(FigureTypeID id, FigureVisualsConfig visualsConfig)
    {
        _figureID = id;

        // Назначаем спрайт для комбинированной формы и цвета
        if (_combinedShapeColorRenderer != null)
        {
            _combinedShapeColorRenderer.sprite = visualsConfig.GetCombinedShapeColorSprite(id.Shape, id.ShapeColor);
            _combinedShapeColorRenderer.color = Color.white; // Цвет должен быть white, так как цвет уже в спрайте
            _combinedShapeColorRenderer.gameObject.layer = LayerMask.NameToLayer("Default");
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_combinedShapeColorRenderer.gameObject);
            #endif
        }
        else Debug.LogError($"Figure: Combined Shape Color Renderer NULL для {id}. Не могу настроить визуал.", this);

        // Назначаем спрайт для животного
        if (_animalRenderer != null)
        {
            _animalRenderer.sprite = visualsConfig.GetAnimalSprite(id.Animal);
            _animalRenderer.color = Color.white; // Цвет животного обычно white
            _animalRenderer.gameObject.layer = LayerMask.NameToLayer("Default");
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_animalRenderer.gameObject);
            #endif
        }
        else Debug.LogError($"Figure: Animal Renderer NULL для {id}. Не могу настроить визуал.", this);

        this.gameObject.layer = LayerMask.NameToLayer("Default");

        _collider2D = GetComponent<PolygonCollider2D>();
        if (_collider2D == null)
        {
            _collider2D = gameObject.AddComponent<PolygonCollider2D>();
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_collider2D);
            #endif
            Debug.Log($"Figure: PolygonCollider2D добавлен для {id}.", this);
        }

        _rigidbody2D = GetComponent<Rigidbody2D>();
        if (_rigidbody2D == null)
        {
            _rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody2D.gravityScale = 1f;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_rigidbody2D);
            #endif
            Debug.Log($"Figure: Rigidbody2D добавлен для {id}.", this);
        }

        // Коллайдер теперь будет использовать спрайт _combinedShapeColorRenderer
        if (_collider2D != null && _combinedShapeColorRenderer.sprite != null)
        {
            Sprite spriteToUseForCollider = _combinedShapeColorRenderer.sprite;
            System.Collections.Generic.List<Vector2> physicsShapeVertices = new System.Collections.Generic.List<Vector2>();

            if (spriteToUseForCollider.GetPhysicsShapeCount() > 0)
            {
                spriteToUseForCollider.GetPhysicsShape(0, physicsShapeVertices);
            }
            else
            {
                Debug.LogWarning($"Спрайт '{spriteToUseForCollider.name}' не имеет своей физ. формы. Используем вершины спрайта для коллайдера {id}.", spriteToUseForCollider);
                physicsShapeVertices.AddRange(spriteToUseForCollider.vertices);
            }

            if (physicsShapeVertices.Count > 0)
            {
                _collider2D.points = physicsShapeVertices.ToArray();
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_collider2D);
                #endif
            }
            else
            {
                Debug.LogWarning($"Figure: Не удалось получить вершины коллайдера для {spriteToUseForCollider.name} для {id}. Используем квадрат.", this);
                _collider2D.points = new Vector2[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(_collider2D);
                #endif
            }
        }
        else
        {
            Debug.LogError($"Figure: Нет критически важных компонентов для {id}. Не могу настроить коллайдер. Проверьте назначения.", this);
        }

        Initialize();
    }

    /// <summary>
    /// Обработка нажатия кнопки мыши на коллайдере фишки.
    /// </summary>
    private void OnMouseDown()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameManager.GameState.Playing)
        {
            return;
        }

        if (_collider2D == null || !_collider2D.enabled || _rigidbody2D == null)
        {
            Debug.LogWarning("Figure: Компоненты отсутствуют или выключены, не могу обработать клик.", this);
            return;
        }

        _mouseDownWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _mouseDownWorldPos.z = transform.position.z;

        _isDragging = false;

        _rigidbody2D.linearVelocity = Vector2.zero;
        _rigidbody2D.angularVelocity = 0f;
    }

    /// <summary>
    /// Обработка перетаскивания кнопки мыши.
    /// </summary>
    private void OnMouseDrag()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameManager.GameState.Playing) return;
        if (_collider2D == null || !_collider2D.enabled || _rigidbody2D == null || _rigidbody2D.isKinematic) return;

        Vector3 currentMouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        currentMouseWorldPos.z = transform.position.z;

        if (!_isDragging && Vector3.Distance(_mouseDownWorldPos, currentMouseWorldPos) > _dragThreshold)
        {
            _isDragging = true;
        }

        if (_isDragging)
        {
            Vector3 forceDirection = (currentMouseWorldPos - (Vector3)_rigidbody2D.position);
            _rigidbody2D.AddForce(forceDirection * _dragForce * Time.fixedDeltaTime, ForceMode2D.Force);
        }
    }

    /// <summary>
    /// Обработка отпускания кнопки мыши.
    /// </summary>
    private void OnMouseUp()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameManager.GameState.Playing) return;
        if (_collider2D == null || !_collider2D.enabled || _rigidbody2D == null) return;

        if (!_isDragging)
        {
            Debug.Log($"Figure: Кликнули по {FigureID.Animal}!");
            OnFigureClicked?.Invoke(this);
        }
        _isDragging = false;
    }

    /// <summary>
    /// Обработка столкновений с другими физическими объектами.
    /// Звук проигрывается только при первом касании после инициализации и после задержки.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Если звук уже проигран для этой фишки, или игра не в нужном состоянии, выходим.
        // Игнорируем столкновения для звука, пока не пройдет задержка после спавна.
        if (_hasPlayedCollisionSound || Time.time < _collisionSoundActivationTime ||
            GameManager.Instance == null ||
            (GameManager.Instance.CurrentGameState != GameManager.GameState.Playing &&
             GameManager.Instance.CurrentGameState != GameManager.GameState.Rerolling))
        {
            return;
        }

        // Воспроизводим звук столкновения.
        GameManager.Instance.PlayCollisionSound();
        _hasPlayedCollisionSound = true; // Устанавливаем флаг, чтобы звук больше не проигрывался.
    }

    /// <summary>
    /// Переводит фишку в режим "панели действий" (отключает физику).
    /// </summary>
    public void SetInActionBarMode()
    {
        if (_rigidbody2D != null)
        {
            _rigidbody2D.isKinematic = true;
            _rigidbody2D.simulated = false;
        }
        if (_collider2D != null) _collider2D.enabled = false;
    }

    /// <summary>
    /// Сбрасывает физическое состояние фишки (для использования на игровом поле).
    /// </summary>
    public void ResetPhysicsState()
    {
        if (_rigidbody2D != null)
        {
            _rigidbody2D.isKinematic = false;
            _rigidbody2D.simulated = true;
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
        }
        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }
    }
}