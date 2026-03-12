using UnityEngine;

namespace AirGuard.Controllers
{
    /// <summary>
    /// 물리 기반 드론 이동 컨트롤러
    /// - Rigidbody 기반 추력/토크
    /// - RC 드론 스타일: 기울기로 이동, 호버링
    /// - WASD: 전후좌우 기울기, Space: 상승, Shift: 하강, Q/E: 좌우 회전(Yaw)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleMovementController : MonoBehaviour
    {
        [Header("추력 설정")]
        [SerializeField] private float hoverThrust = 9.81f;
        [SerializeField] private float maxThrust = 25f;
        [SerializeField] private float thrustSensitivity = 8f;

        [Header("기울기(틸트) 설정")]
        [SerializeField] private float maxTiltAngle = 28f;
        [SerializeField] private float tiltSpeed = 4f;
        [SerializeField] private float moveForce = 18f;

        [Header("Yaw 설정")]
        [SerializeField] private float yawSpeed = 90f;

        [Header("안정화")]
        [SerializeField] private float angularDrag = 4f;
        [SerializeField] private float linearDrag = 1.2f;
        [SerializeField] private float autoLevelSpeed = 5f;

        // ─── 내부 상태 ───
        private Rigidbody _rb;
        private float _thrustInput = 0f;
        private float _currentThrust = 0f;
        private Vector2 _tiltTarget = Vector2.zero;
        private float _yawInput = 0f;
        private bool _engineOn = true;

        // ─── 공개 속성 ───
        public float CurrentSpeed { get; private set; }
        public bool IsMoving { get; private set; }
        public bool EngineOn => _engineOn;
        public float CurrentThrust => _currentThrust;
        public float MaxThrust => maxThrust;

        // 미션 실행 중 수동 조작 비활성화
        public bool ExternalControl { get; set; } = false;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = true;
            _rb.mass = 1.5f;
            _rb.linearDamping = linearDrag;
            _rb.angularDamping = angularDrag;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.None;
        }

        private void Update()
        {
            if (ExternalControl) return;
            ReadInput();
        }

        private void FixedUpdate()
        {
            if (ExternalControl) return;
            ApplyThrust();
            ApplyTilt();
            ApplyYaw();
            AutoLevel();
            UpdateSpeed();
        }

        private void ReadInput()
        {
            _thrustInput = 0f;
            if (Input.GetKey(KeyCode.Space)) _thrustInput = 1f;
            if (Input.GetKey(KeyCode.LeftShift)) _thrustInput = -1f;

            float pitch = Input.GetAxis("Vertical");
            float roll = Input.GetAxis("Horizontal");
            _tiltTarget = new Vector2(pitch, roll);

            _yawInput = 0f;
            if (Input.GetKey(KeyCode.Q)) _yawInput = -1f;
            if (Input.GetKey(KeyCode.E)) _yawInput = 1f;
        }

        private void ApplyThrust()
        {
            if (!_engineOn) return;
            float targetThrust = hoverThrust + _thrustInput * (maxThrust - hoverThrust);
            _currentThrust = Mathf.Lerp(_currentThrust, targetThrust, thrustSensitivity * Time.fixedDeltaTime);
            _rb.AddForce(transform.up * _currentThrust, ForceMode.Force);
        }

        private void ApplyTilt()
        {
            if (!_engineOn) return;
            if (_tiltTarget.magnitude < 0.01f) return;
            Vector3 force = (transform.forward * _tiltTarget.x + transform.right * _tiltTarget.y) * moveForce;
            _rb.AddForce(force, ForceMode.Force);
        }

        private void ApplyYaw()
        {
            if (_yawInput == 0f) return;
            _rb.AddTorque(Vector3.up * _yawInput * yawSpeed * Time.fixedDeltaTime, ForceMode.VelocityChange);
        }

        private void AutoLevel()
        {
            if (_tiltTarget.magnitude > 0.05f) return;
            Quaternion currentRot = transform.rotation;
            Quaternion targetRot = Quaternion.Euler(0f, currentRot.eulerAngles.y, 0f);
            transform.rotation = Quaternion.Slerp(currentRot, targetRot, autoLevelSpeed * Time.fixedDeltaTime);
        }

        private void UpdateSpeed()
        {
            CurrentSpeed = _rb.linearVelocity.magnitude * 3.6f;
            IsMoving = CurrentSpeed > 0.5f;
        }

        public void SetEngineOn(bool on) => _engineOn = on;
        public void SetMoveSpeed(float s) => moveForce = s;
        public void SetRotationSpeed(float s) => yawSpeed = s;

        public void MoveToward(Vector3 target, float speed)
        {
            _rb.MovePosition(Vector3.MoveTowards(
                _rb.position, target, speed * Time.fixedDeltaTime));
        }
    }
}