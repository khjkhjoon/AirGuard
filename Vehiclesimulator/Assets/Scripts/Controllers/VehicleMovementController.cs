using UnityEngine;

namespace AirGuard.Controllers
{
    /// <summary>
    /// 차량 이동 제어
    /// WASD: 전후좌우, Space: 상승, Shift: 하강
    /// </summary>
    public class VehicleMovementController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private bool allowVerticalMovement = true;

        private Vector3 _lastPosition;
        private float _currentSpeed;

        public float CurrentSpeed => _currentSpeed;
        public bool IsMoving { get; private set; }

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            HandleMovement();
            CalculateSpeed();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            IsMoving = false;

            // 전후좌우 이동
            if (horizontal != 0 || vertical != 0)
            {
                Vector3 movement = new Vector3(horizontal, 0, vertical).normalized * moveSpeed * Time.deltaTime;
                transform.Translate(movement, Space.World);

                // 이동 방향으로 회전
                if (movement != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(movement);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        targetRotation,
                        rotationSpeed * Time.deltaTime
                    );
                }

                IsMoving = true;
            }

            // 수직 이동 (드론용)
            if (allowVerticalMovement)
            {
                if (Input.GetKey(KeyCode.Space))
                {
                    transform.Translate(Vector3.up * moveSpeed * Time.deltaTime, Space.World);
                    IsMoving = true;
                }

                if (Input.GetKey(KeyCode.LeftShift))
                {
                    transform.Translate(Vector3.down * moveSpeed * Time.deltaTime, Space.World);
                    IsMoving = true;
                }
            }
        }

        private void CalculateSpeed()
        {
            float distance = Vector3.Distance(transform.position, _lastPosition);
            _currentSpeed = (distance / Time.deltaTime) * 3.6f; // m/s → km/h
            _lastPosition = transform.position;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }
    }
}