// ActionBarManager.cs
// Управляет панелью, куда игрок собирает фишки.
// Проверяет совпадения и убирает фишки.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 
using System.Linq; 

[RequireComponent(typeof(AudioSource))]
public class ActionBarManager : MonoBehaviour
{
    public static ActionBarManager Instance { get; private set; }

    [Header("Элементы UI")]
    [SerializeField] private RectTransform[] _figureSlots; 
    [SerializeField] private Image _actionBarBackgroundImage; // Фоновое изображение Action Bar
    
    [Header("Настройки")]
    [SerializeField] private float _figureMoveSpeed = 15f; 
    [SerializeField] [Tooltip("Конечный масштаб фишки в баре (например, 0.7f).")]
    private float _figureScaleInBar = 0.7f; 
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Как плавно движется фишка

    [Header("Настройки анимации уничтожения совпавших фишек")]
    [SerializeField] private float _matchRemovalAnimationDuration = 0.3f; 
    [SerializeField] private float _matchRemovalDelayBetweenFigures = 0.1f; 
    [SerializeField] private float _matchRemovalDelayBeforeShift = 0.1f; 
    [SerializeField] private float _matchFoundHoldDuration = 1.0f; 
    [SerializeField] private AnimationCurve _destroyAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); 

    [Header("Настройки вспышки комбо")] 
    [SerializeField] private Color _comboFlashColor = Color.white; 
    [SerializeField] private float _comboFlashDuration = 0.2f; 
    [SerializeField] private AnimationCurve _comboFlashCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); 
    [SerializeField] [Tooltip("Насколько ярче будет вспышка. 1.0 - обычная яркость, 2.0 - в два раза ярче.")]
    private float _comboBrightnessMultiplier = 2.0f; 

    [Header("Настройки очистки бара (для обновления уровня)")]
    [SerializeField] private float _clearFigureAnimationDuration = 0.2f; 
    [SerializeField] private float _clearFigureDelayBetween = 0.05f; 

    [Header("Звуки и эффекты")]
    [SerializeField] private AudioClip _matchSound;
    [SerializeField] private ParticleSystem _matchParticlesSystem; 
    private AudioSource _audioSource; 

    private List<Figure> _figuresInBar = new List<Figure>(); 
    private const int MAX_SLOTS = 7; 

    private Color _originalBarColor; 
    private Coroutine _currentFlashCoroutine; 
    private bool _matchSoundPlayedForCurrentComboChain = false; 

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        _audioSource = GetComponent<AudioSource>(); 
        if (_audioSource == null) Debug.LogError("[Бар] Нет AudioSource на ActionBarManager!");

        if (_figureSlots == null || _figureSlots.Length != MAX_SLOTS)
        {
            Debug.LogError($"[Бар] Нужно назначить {MAX_SLOTS} слотов в инспекторе!");
        }

        // --- ИНИЦИАЛИЗАЦИЯ КРИВЫХ (если их нет в вашем оригинале, вы можете удалить эти блоки) ---
        if (_destroyAnimationCurve.keys.Length == 0) 
        {
            _destroyAnimationCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); 
            Debug.Log("[Бар] _destroyAnimationCurve инициализирован по умолчанию.");
        }

        if (_actionBarBackgroundImage != null)
        {
            _originalBarColor = _actionBarBackgroundImage.color;
        }
        else
        {
            Debug.LogWarning("[Бар] Не назначено фоновое изображение бара (_actionBarBackgroundImage). Вспышка комбо не будет работать.");
        }

        if (_comboFlashCurve.keys.Length == 0) 
        {
            _comboFlashCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),    
                new Keyframe(0.5f, 1f, 0f, 0f),  
                new Keyframe(1f, 0f, 0f, 0f)     
            );
            Debug.Log("[Бар] _comboFlashCurve инициализирован по умолчанию.");
        }
        // --- КОНЕЦ ИНИЦИАЛИЗАЦИИ КРИВЫХ ---
    }
    /// <summary>
    /// Добавляет фишку в бар.
    /// </summary>
    public void AddFigureToBar(Figure figure)
    {
        if (figure == null) return;

        if (_figuresInBar.Count >= MAX_SLOTS)
        {
            Debug.Log("[Бар] Бар полон. Игра окончена (проигрыш).");
            GameManager.Instance?.GameOver(false); 
            ObjectPoolManager.Instance?.ReturnFigure(figure); 
            return; 
        }

        if (_figuresInBar.Contains(figure)) 
        {
            Debug.LogWarning($"[Бар] Фишка {figure.name} уже в баре. Пропускаем.");
            return; 
        }

        _figuresInBar.Add(figure); 
        figure.SetInActionBarMode(); 

        _matchSoundPlayedForCurrentComboChain = false; // Сброс флага для звука
        
        int targetSlotIndex = MAX_SLOTS - 1 - (_figuresInBar.Count - 1); 
        
        StartCoroutine(MoveFigureToSlotCoroutine(figure, targetSlotIndex)); 
    }

    /// <summary>
    /// Корутина для плавного перемещения фишки в слот.
    /// </summary>
    private IEnumerator MoveFigureToSlotCoroutine(Figure figure, int slotIndex)
    {
        if (figure == null) yield break; 

        if (slotIndex < 0 || slotIndex >= MAX_SLOTS)
        {
            Debug.LogError($"[Бар] Неправильный индекс слота: {slotIndex}");
            yield break;
        }

        RectTransform targetSlotRect = _figureSlots[slotIndex]; 
        
        Vector3[] corners = new Vector3[4];
        targetSlotRect.GetWorldCorners(corners);
        Vector3 targetPositionWorld = (corners[0] + corners[2]) / 2f; 
        targetPositionWorld.z = 0f; 

        Vector3 targetScale = Vector3.one * _figureScaleInBar; 
        Quaternion targetRotation = Quaternion.identity; 

        Vector3 startPosition = figure.transform.position;
        Quaternion startRotation = figure.transform.rotation; 
        Vector3 startScale = figure.transform.localScale; 
        
        float journeyLength = Vector3.Distance(figure.transform.position, targetPositionWorld);
        float duration = journeyLength / _figureMoveSpeed; 
        float startTime = Time.time;
        
        while (Vector3.Distance(figure.transform.position, targetPositionWorld) > 0.01f || 
               Quaternion.Angle(figure.transform.rotation, targetRotation) > 0.1f || 
               Vector3.Distance(figure.transform.localScale, targetScale) > 0.001f) 
        {
            float elapsed = Time.time - startTime;
            float linearT = Mathf.Clamp01(elapsed / duration); 
            
            float curvedT = _moveCurve.Evaluate(linearT); 

            figure.transform.position = Vector3.Lerp(startPosition, targetPositionWorld, curvedT);
            figure.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);
            figure.transform.localScale = Vector3.Lerp(startScale, targetScale, curvedT);
            yield return null;
        }
        
        figure.transform.position = targetPositionWorld;
        figure.transform.rotation = targetRotation; 
        figure.transform.localScale = targetScale; 

        figure.transform.SetParent(targetSlotRect, worldPositionStays: true); 
        
        if (figure.TryGetComponent<RectTransform>(out RectTransform figureRectTransform))
        {
            figureRectTransform.anchoredPosition3D = Vector3.zero; 
        }
        else
        {
            figure.transform.localPosition = Vector3.zero; 
        }
        
        // Если у вас есть проблемы с тем, что масштаб "зависает" в некорректном значении после SetParent,
        // здесь можно было бы добавить:
        // yield return new WaitForEndOfFrame(); 
        // figure.transform.localScale = targetScale; 
        // Но если это нарушает плавность, которую вы хотите, то это нужно исследовать отдельно.

        yield return StartCoroutine(CheckForMatchesCoroutine());
    }

    /// <summary>
    /// Корутина для проверки и удаления совпадений.
    /// </summary>
    private IEnumerator CheckForMatchesCoroutine()
    {
        // Разрешить проверку при реролле (если это нужно)
        if (GameManager.Instance != null && GameManager.Instance.CurrentGameState != GameManager.GameState.Playing &&
            GameManager.Instance.CurrentGameState != GameManager.GameState.Rerolling) 
        {
            yield break;
        }

        bool matchFoundInLoop;
        do
        {
            matchFoundInLoop = false;
            if (_figuresInBar.Count < 3) break; 

            // ВОЗВРАЩАЕМСЯ К ВАШЕЙ ОРИГИНАЛЬНОЙ ЛОГИКЕ ПОИСКА СОВПАДЕНИЙ ПОДРЯД
            List<Figure> currentBarState = new List<Figure>(_figuresInBar); 
            for (int i = 0; i <= currentBarState.Count - 3; i++)
            {
                Figure fig1 = currentBarState[i];
                Figure fig2 = currentBarState[i + 1];
                Figure fig3 = currentBarState[i + 2];

                if (fig1 != null && fig2 != null && fig3 != null &&
                    fig1.FigureID == fig2.FigureID && fig2.FigureID == fig3.FigureID)
                {
                    Debug.Log($"[Бар] Найдено совпадение: {fig1.FigureID}!");
                    matchFoundInLoop = true; 
                    
                    if (_actionBarBackgroundImage != null)
                    {
                        if (_currentFlashCoroutine != null) StopCoroutine(_currentFlashCoroutine);
                        _currentFlashCoroutine = StartCoroutine(AnimateComboFlash(_actionBarBackgroundImage, _originalBarColor, _comboFlashColor, _comboFlashDuration, _comboFlashCurve, _comboBrightnessMultiplier));
                    }

                    // Передаем оригинальный список из трех фишек для удаления
                    // Важно передавать их в том порядке, в котором они были найдены,
                    // чтобы AnimateAndRemoveMatchedFigures мог правильно их обработать.
                    yield return StartCoroutine(AnimateAndRemoveMatchedFigures(new List<Figure> { fig1, fig2, fig3 }));
                    break; // Выходим из for, чтобы начать проверку заново после удаления
                }
            }
        } while (matchFoundInLoop); 

        // Проверка на проигрыш после всех возможных совпадений.
        if (_figuresInBar.Count >= MAX_SLOTS)
        {
            Debug.Log("[Бар] Бар полон и нет совпадений. Проигрыш.");
            GameManager.Instance?.GameOver(false); 
        }
    }

    /// <summary>
    /// Корутина для анимации и удаления совпавших фишек.
    /// </summary>
    private IEnumerator AnimateAndRemoveMatchedFigures(List<Figure> matchedFigures) // Принимает список
    {
        yield return new WaitForSeconds(_matchFoundHoldDuration); 
        
        // Проигрываем звук, только если он еще не проигрывался в этой цепочке
        if (!_matchSoundPlayedForCurrentComboChain && _audioSource != null && _matchSound != null)
        {
            _audioSource.PlayOneShot(_matchSound);
            _matchSoundPlayedForCurrentComboChain = true; 
        }

        if (_matchParticlesSystem != null)
        {
            Vector3 particlePosition = Vector3.zero;
            foreach (Figure fig in matchedFigures)
            {
                if (fig != null) particlePosition += fig.transform.position;
            }
            if (matchedFigures.Count > 0) particlePosition /= matchedFigures.Count;

            _matchParticlesSystem.transform.position = particlePosition;

            _matchParticlesSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); 
            _matchParticlesSystem.Play(); 
        }
        else
        {
            Debug.LogWarning("[Бар] Нет ParticleSystem для эффекта совпадения!");
        }

        // --- ВОЗВРАЩАЕМСЯ К ВАШЕЙ ЛОГИКЕ: АНИМАЦИЯ УДАЛЕНИЯ СЛЕВА НАПРАВО ---
        // Если matchedFigures пришел в порядке (правая, средняя, левая) из-за того, как 
        // fig1, fig2, fig3 были выбраны из currentBarState (который является копией _figuresInBar,
        // а _figuresInBar заполняется СПРАВА НАЛЕВО),
        // то для анимации СЛЕВА НАПРАВО нам нужно развернуть этот список.
        List<Figure> figuresToAnimate = new List<Figure>(matchedFigures); // Создаем копию для разворота
        figuresToAnimate.Reverse(); // <--- ВАЖНО: РАЗВОРАЧИВАЕМ СПИСОК ДЛЯ АНИМАЦИИ СЛЕВА НАПРАВО

        foreach (Figure fig in figuresToAnimate) 
        {
            if (fig != null)
            {
                yield return StartCoroutine(AnimateFigureScaleDownAndReturn(fig, _matchRemovalAnimationDuration));
            }
            yield return new WaitForSeconds(_matchRemovalDelayBetweenFigures); 
        }

        // Удаляем из _figuresInBar из оригинального списка
        // Важно делать это после анимации
        foreach (Figure fig in matchedFigures) 
        {
            if (_figuresInBar.Contains(fig)) 
            {
                _figuresInBar.Remove(fig);
            }
        }

        // startIndex здесь не нужен, так как ShiftFiguresCoroutine теперь сдвигает все фишки в баре
        yield return StartCoroutine(ShiftFiguresCoroutine()); 
    }

    /// <summary>
    /// Корутина для сдвига оставшихся фишек влево.
    /// </summary>
    private IEnumerator ShiftFiguresCoroutine() // startIndex удален
    {
        yield return new WaitForSeconds(_matchRemovalDelayBeforeShift); 

        Vector3 targetScale = Vector3.one * _figureScaleInBar; 

        for (int i = 0; i < _figuresInBar.Count; i++)
        {
            Figure figure = _figuresInBar[i];
            if (figure == null) continue; 

            int newTargetSlotIndex = MAX_SLOTS - 1 - i; 
            RectTransform targetSlotRect = _figureSlots[newTargetSlotIndex]; 

            Vector3 startShiftPosition = figure.transform.position;
            Quaternion startShiftRotation = figure.transform.rotation;
            
            Vector3[] corners = new Vector3[4];
            targetSlotRect.GetWorldCorners(corners);
            Vector3 targetPositionWorld = (corners[0] + corners[2]) / 2f;
            targetPositionWorld.z = 0f; 

            Quaternion targetRotation = Quaternion.identity; 

            float shiftDuration = 0.2f; 
            float startTime = Time.time;

            while (Vector3.Distance(figure.transform.position, targetPositionWorld) > 0.01f || Quaternion.Angle(figure.transform.rotation, targetRotation) > 0.1f)
            {
                float elapsedTime = Time.time - startTime;
                float fractionOfJourney = Mathf.Clamp01(elapsedTime / shiftDuration);
                
                figure.transform.position = Vector3.Lerp(startShiftPosition, targetPositionWorld, fractionOfJourney);
                figure.transform.rotation = Quaternion.Slerp(startShiftRotation, targetRotation, fractionOfJourney); 
                yield return null;
            }
            figure.transform.position = targetPositionWorld;
            figure.transform.rotation = targetRotation; 

            figure.transform.SetParent(targetSlotRect, worldPositionStays: true);
            if (figure.TryGetComponent<RectTransform>(out RectTransform figureRectTransform))
            {
                figureRectTransform.anchoredPosition3D = Vector3.zero;
            }
            else
            {
                figure.transform.localPosition = Vector3.zero;
            }
            // Если у вас есть проблемы с тем, что масштаб "зависает" в некорректном значении после SetParent,
            // здесь можно было бы добавить:
            // yield return new WaitForEndOfFrame(); 
            // figure.transform.localScale = targetScale;
        }
    }

    /// <summary>
    /// Полностью очищает бар с анимацией.
    /// </summary>
    public IEnumerator ClearActionBarAnimated()
    {
        List<Figure> figuresToClearCopy = new List<Figure>(_figuresInBar);
        
        for (int i = figuresToClearCopy.Count - 1; i >= 0; i--)
        {
            Figure figureToClear = figuresToClearCopy[i];
            if (figureToClear != null)
            {
                yield return StartCoroutine(AnimateFigureScaleDownAndReturn(figureToClear, _clearFigureAnimationDuration)); 
            }
            yield return new WaitForSeconds(_clearFigureDelayBetween); 
        }
        _figuresInBar.Clear(); 
        Debug.Log("[Бар] Панель очищена.");
    }

    /// <summary>
    /// Анимирует изменение масштаба фишки и возвращает её в пул.
    /// </summary>
    /// <param name="figure">Фишка для анимации.</param>
    /// <param name="duration">Длительность анимации.</param>
    private IEnumerator AnimateFigureScaleDownAndReturn(Figure figure, float duration)
    {
        if (figure == null) yield break;

        Vector3 startScale = figure.transform.localScale;
        float startTime = Time.time;

        while (Time.time < startTime + duration)
        {
            float t = (Time.time - startTime) / duration;
            float curveProgress = _destroyAnimationCurve.Evaluate(t);
            if (figure != null) 
            {
                figure.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, curveProgress);
            }
            yield return null;
        }

        if (figure != null) 
        {
            figure.transform.localScale = Vector3.zero; 
            ObjectPoolManager.Instance?.ReturnFigure(figure); 
        }
    }

    /// <summary>
    /// Корутина для анимации вспышки фона бара.
    /// </summary>
    private IEnumerator AnimateComboFlash(Image targetImage, Color startColor, Color flashColor, float duration, AnimationCurve curve, float brightnessMultiplier)
    {
        if (targetImage == null) yield break;

        float timer = 0f;
        if (_currentFlashCoroutine != null) StopCoroutine(_currentFlashCoroutine); // Останавливаем предыдущую вспышку, если она еще идет
        
        Color peakFlashColor = flashColor * brightnessMultiplier;

        while (timer < duration)
        {
            float t = timer / duration;
            float curveValue = curve.Evaluate(t); 

            targetImage.color = Color.Lerp(startColor, peakFlashColor, curveValue);
            timer += Time.deltaTime;
            yield return null;
        }
        targetImage.color = startColor;
        _currentFlashCoroutine = null; // Сброс ссылки на корутину вспышки
    }

    /// <summary>
    /// Полностью сбрасывает состояние бара без анимации.
    /// </summary>
    public void ResetBar()
    {
        // Используем копию, чтобы избежать проблем при изменении коллекции во время итерации
        List<Figure> figuresInBarCopy = new List<Figure>(_figuresInBar);
        foreach(Figure figure in figuresInBarCopy)
        {
            if(figure != null) ObjectPoolManager.Instance?.ReturnFigure(figure);
        }
        _figuresInBar.Clear(); 
        Debug.Log("[Бар] Панель сброшена.");
    }
}