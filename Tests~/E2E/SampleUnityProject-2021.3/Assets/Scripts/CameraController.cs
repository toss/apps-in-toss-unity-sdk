using UnityEngine;

/// <summary>
/// 간단한 카메라 컨트롤러
/// 마우스와 키보드로 카메라를 움직입니다.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 20f;
    public float mouseSensitivity = 2f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 20f;
    public float maxZoom = 90f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        // 초기 회전값 설정
        Vector3 rotation = transform.localRotation.eulerAngles;
        rotationY = rotation.y;
        rotationX = rotation.x;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    void HandleMovement()
    {
        // 이동 속도 (Shift로 빠르게 이동)
        float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;

        // WASD 이동
        if (Input.GetKey(KeyCode.W))
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.position -= transform.forward * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.position -= transform.right * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.position += transform.right * speed * Time.deltaTime;
        }

        // Q/E로 상하 이동
        if (Input.GetKey(KeyCode.Q))
        {
            transform.position -= transform.up * speed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.position += transform.up * speed * Time.deltaTime;
        }
    }

    void HandleRotation()
    {
        // 우클릭 드래그로 회전
        if (Input.GetMouseButton(1))
        {
            rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
            rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
        }
    }

    void HandleZoom()
    {
        if (cam != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                cam.fieldOfView = Mathf.Clamp(
                    cam.fieldOfView - scroll * zoomSpeed * 10f,
                    minZoom,
                    maxZoom
                );
            }
        }
    }
}
