using UnityEngine;

/// <summary>
/// IMGUI용 터치/마우스 스크롤 핸들러
/// 터치 입력, 마우스 드래그, 관성 스크롤을 지원
/// </summary>
public class TouchScrollHandler
{
    // 스크롤 상태
    private bool _isTouchScrolling = false;
    private bool _isDragging = false;
    private Vector2 _touchStartPosition;
    private Vector2 _lastTouchPosition;
    private Vector2 _scrollVelocity = Vector2.zero;

    // 설정
    private readonly float _momentumDecay;
    private readonly float _dragThreshold;

    // 현재 스크롤 영역
    private Rect _scrollViewRect;

    /// <summary>
    /// 현재 스크롤 위치
    /// </summary>
    public Vector2 ScrollPosition { get; set; } = Vector2.zero;

    /// <summary>
    /// 드래그 중인지 여부 (버튼 클릭 차단용)
    /// </summary>
    public bool IsDragging => _isDragging;

    /// <summary>
    /// 스크롤 관성이 활성화되어 있는지
    /// </summary>
    public bool HasMomentum => _scrollVelocity.sqrMagnitude > 0.01f;

    /// <summary>
    /// TouchScrollHandler 생성자
    /// </summary>
    /// <param name="momentumDecay">관성 감쇠 계수 (0.0 ~ 1.0, 기본값 0.95)</param>
    /// <param name="dragThreshold">드래그 인식 임계값 (픽셀, 기본값 10)</param>
    public TouchScrollHandler(float momentumDecay = 0.95f, float dragThreshold = 10f)
    {
        _momentumDecay = momentumDecay;
        _dragThreshold = dragThreshold;
    }

    /// <summary>
    /// 스크롤 영역 설정 (OnGUI에서 호출)
    /// </summary>
    public void SetScrollViewRect(Rect rect)
    {
        _scrollViewRect = rect;
    }

    /// <summary>
    /// Update에서 호출하여 터치/마우스 입력 처리
    /// </summary>
    public void HandleInput()
    {
        HandleTouchScroll();
        ApplyScrollMomentum();
    }

    /// <summary>
    /// 스크롤 중 버튼 클릭을 차단해야 하는지 여부
    /// </summary>
    public bool ShouldBlockInput()
    {
        return _isDragging || _scrollVelocity.sqrMagnitude > 1f;
    }

    /// <summary>
    /// 스크롤 위치 초기화
    /// </summary>
    public void ResetScroll()
    {
        ScrollPosition = Vector2.zero;
        _scrollVelocity = Vector2.zero;
        _isTouchScrolling = false;
        _isDragging = false;
    }

    /// <summary>
    /// 터치 스크롤 처리
    /// </summary>
    private void HandleTouchScroll()
    {
        // 터치 입력 처리
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // 스크롤 영역 내에서 터치 시작했는지 확인
                    Vector2 touchPos = new Vector2(touch.position.x, Screen.height - touch.position.y);
                    if (_scrollViewRect.Contains(touchPos))
                    {
                        _isTouchScrolling = true;
                        _isDragging = false;
                        _touchStartPosition = touch.position;
                        _lastTouchPosition = touch.position;
                        _scrollVelocity = Vector2.zero;
                    }
                    break;

                case TouchPhase.Moved:
                    if (_isTouchScrolling)
                    {
                        // 드래그 임계값 확인
                        float totalDragDistance = Vector2.Distance(touch.position, _touchStartPosition);
                        if (!_isDragging && totalDragDistance > _dragThreshold)
                        {
                            _isDragging = true;
                        }

                        Vector2 delta = touch.position - _lastTouchPosition;
                        ScrollPosition = new Vector2(ScrollPosition.x, ScrollPosition.y + delta.y);
                        ScrollPosition = new Vector2(ScrollPosition.x, Mathf.Max(0, ScrollPosition.y));

                        _scrollVelocity = new Vector2(0, delta.y) / Time.deltaTime * 0.1f;
                        _lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    _isTouchScrolling = false;
                    _isDragging = false;
                    break;
            }
        }
        // 마우스 드래그 지원 (WebGL 데스크톱 테스트용)
        else if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            if (Input.GetMouseButtonDown(0))
            {
                if (_scrollViewRect.Contains(mousePos))
                {
                    _isTouchScrolling = true;
                    _isDragging = false;
                    _touchStartPosition = Input.mousePosition;
                    _lastTouchPosition = Input.mousePosition;
                    _scrollVelocity = Vector2.zero;
                }
            }
            else if (_isTouchScrolling)
            {
                // 드래그 임계값 확인
                float totalDragDistance = Vector2.Distance(Input.mousePosition, _touchStartPosition);
                if (!_isDragging && totalDragDistance > _dragThreshold)
                {
                    _isDragging = true;
                }

                Vector2 delta = (Vector2)Input.mousePosition - _lastTouchPosition;
                ScrollPosition = new Vector2(ScrollPosition.x, ScrollPosition.y + delta.y);
                ScrollPosition = new Vector2(ScrollPosition.x, Mathf.Max(0, ScrollPosition.y));

                _scrollVelocity = new Vector2(0, delta.y) / Time.deltaTime * 0.1f;
                _lastTouchPosition = Input.mousePosition;
            }
        }
        else
        {
            if (_isTouchScrolling)
            {
                _isTouchScrolling = false;
                _isDragging = false;
            }
        }
    }

    /// <summary>
    /// 스크롤 관성 적용
    /// </summary>
    private void ApplyScrollMomentum()
    {
        if (!_isTouchScrolling && _scrollVelocity.sqrMagnitude > 0.01f)
        {
            ScrollPosition = new Vector2(
                ScrollPosition.x,
                Mathf.Max(0, ScrollPosition.y + _scrollVelocity.y * Time.deltaTime)
            );
            _scrollVelocity *= _momentumDecay;

            if (_scrollVelocity.sqrMagnitude < 0.01f)
            {
                _scrollVelocity = Vector2.zero;
            }
        }
    }
}
