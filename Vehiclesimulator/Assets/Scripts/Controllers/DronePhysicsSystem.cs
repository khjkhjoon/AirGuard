using UnityEngine;

namespace AirGuard.Controllers
{
    /// <summary>
    /// 드론 부가 물리 시스템
    /// - 바람/외력
    /// - 엔진 소리
    /// - 추락/충돌 처리
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AudioSource))]
    public class DronePhysicsSystem : MonoBehaviour
    {
        // ══════════════════════════════════════════
        // 바람 설정
        // ══════════════════════════════════════════
        [Header("바람 설정")]
        [SerializeField] private bool enableWind = true;
        [SerializeField] private float windBaseStrength = 2f;    // 기본 바람 세기 (N)
        [SerializeField] private float windGustStrength = 5f;    // 돌풍 최대 세기
        [SerializeField] private float windChangeSpeed = 0.3f;  // 바람 방향 변화 속도
        [SerializeField] private float gustInterval = 4f;    // 돌풍 주기 (초)

        // ══════════════════════════════════════════
        // 엔진 소리
        // ══════════════════════════════════════════
        [Header("엔진 소리")]
        [SerializeField] private bool enableAudio = true;
        [SerializeField] private float idlePitch = 0.6f;  // 대기 피치
        [SerializeField] private float maxPitch = 1.8f;  // 최대 피치
        [SerializeField] private float idleVolume = 0.3f;
        [SerializeField] private float maxVolume = 0.9f;
        [SerializeField] private float audioSmoothSpeed = 5f;

        // ══════════════════════════════════════════
        // 프로펠러
        // ══════════════════════════════════════════
        [Header("프로펠러")]
        [SerializeField] private Transform[] propellers;
        [SerializeField] private float propellerIdleRPM = 1000f;
        [SerializeField] private float propellerMaxRPM = 8000f;

        // ══════════════════════════════════════════
        // 추락/충돌
        // ══════════════════════════════════════════
        [Header("추락/충돌")]
        [SerializeField] private float crashSpeedThreshold = 8f;   // 이 속도 이상으로 충돌 시 추락
        [SerializeField] private float minAltitude = 0f;   // 최저 고도 (지면)
        [SerializeField] private bool destroyOnCrash = false; // 충돌 시 파괴 여부
        [SerializeField] private GameObject crashEffect;            // 충돌 이펙트 프리팹

        // ══════════════════════════════════════════
        // 내부 상태
        // ══════════════════════════════════════════
        private Rigidbody _rb;
        private AudioSource _audio;
        private VehicleMovementController _movement;

        private Vector3 _windDirection = Vector3.right;
        private float _windNoise = 0f;
        private float _gustTimer = 0f;
        private float gustStrength = 0f;
        private bool _isCrashed = false;

        // 공개 상태
        public bool IsCrashed => _isCrashed;
        public float WindSpeed => (_windDirection * (windBaseStrength + gustStrength)).magnitude;
        public Vector3 WindDirection => _windDirection;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _audio = GetComponent<AudioSource>();
            _movement = GetComponent<VehicleMovementController>();
        }

        private void Start()
        {
            // 오디오 초기화
            if (enableAudio && _audio != null)
            {
                _audio.loop = true;
                _audio.spatialBlend = 1f;  // 3D 사운드
                _audio.pitch = idlePitch;
                _audio.volume = idleVolume;
                if (_audio.clip != null) _audio.Play();
            }

            // 첫 바람 방향 랜덤 설정
            _windDirection = Random.insideUnitSphere;
            _windDirection.y = 0;
            _windDirection.Normalize();
        }

        private void Update()
        {
            if (_isCrashed) return;
            UpdateAudio();
            SpinPropellers();
        }

        private void FixedUpdate()
        {
            if (_isCrashed) return;
            ApplyWind();
            EnforceAltitudeLimit();
        }

        // ══════════════════════════════════════════
        // 바람
        // ══════════════════════════════════════════
        private void ApplyWind()
        {
            if (!enableWind) return;

            // 바람 방향 서서히 변화 (Perlin Noise)
            _windNoise += Time.fixedDeltaTime * windChangeSpeed;
            float noiseX = Mathf.PerlinNoise(_windNoise, 0f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(0f, _windNoise) * 2f - 1f;
            _windDirection = Vector3.Lerp(_windDirection,
                new Vector3(noiseX, 0, noiseZ).normalized,
                Time.fixedDeltaTime * 0.5f);

            // 돌풍 처리
            _gustTimer += Time.fixedDeltaTime;
            if (_gustTimer >= gustInterval)
            {
                _gustTimer = 0f;
                gustStrength = Random.Range(0f, windGustStrength);
                // 돌풍은 서서히 사라짐
            }
            gustStrength = Mathf.Lerp(gustStrength, 0f, Time.fixedDeltaTime * 2f);

            // 바람 힘 적용 (드론이 높을수록 바람 강해짐)
            float altitudeFactor = Mathf.Clamp01(transform.position.y / 20f) * 0.5f + 0.5f;
            Vector3 windForce = _windDirection * (windBaseStrength + gustStrength) * altitudeFactor;

            // 약간의 수직 난기류
            float turbulence = (Mathf.PerlinNoise(_windNoise * 2f, 0.5f) - 0.5f) * 1.5f;
            windForce.y += turbulence;

            _rb.AddForce(windForce, ForceMode.Force);
        }

        // ══════════════════════════════════════════
        // 엔진 소리
        // ══════════════════════════════════════════
        private void UpdateAudio()
        {
            if (!enableAudio || _audio == null || _audio.clip == null) return;

            // 속도에 따라 피치/볼륨 변화
            float speedFactor = Mathf.Clamp01((_movement != null ? _movement.CurrentSpeed : 0f) / 100f);
            float engineFactor = _movement != null && _movement.EngineOn ? 1f : 0f;

            float targetPitch = Mathf.Lerp(idlePitch, maxPitch, speedFactor) * engineFactor;
            float targetVolume = Mathf.Lerp(idleVolume, maxVolume, speedFactor) * engineFactor;

            _audio.pitch = Mathf.Lerp(_audio.pitch, targetPitch, audioSmoothSpeed * Time.deltaTime);
            _audio.volume = Mathf.Lerp(_audio.volume, targetVolume, audioSmoothSpeed * Time.deltaTime);

            if (engineFactor > 0 && !_audio.isPlaying) _audio.Play();
            if (engineFactor == 0 && _audio.isPlaying) _audio.Stop();
        }

        // ══════════════════════════════════════════
        // 고도 제한
        // ══════════════════════════════════════════
        private void EnforceAltitudeLimit()
        {
            if (transform.position.y < minAltitude)
            {
                Vector3 pos = transform.position;
                pos.y = minAltitude;
                transform.position = pos;

                // 하강 속도 제거
                Vector3 vel = _rb.linearVelocity;
                if (vel.y < 0) vel.y = 0;
                _rb.linearVelocity = vel;
            }
        }

        // ══════════════════════════════════════════
        // 충돌 처리
        // ══════════════════════════════════════════
        private void OnCollisionEnter(Collision collision)
        {
            if (_isCrashed) return;

            float impactSpeed = collision.relativeVelocity.magnitude;

            if (impactSpeed >= crashSpeedThreshold)
            {
                TriggerCrash(collision.contacts[0].point);
            }
        }

        private void TriggerCrash(Vector3 position)
        {
            _isCrashed = true;
            Debug.LogWarning($"[DronePhysics] 충돌 추락! 속도: {_rb.linearVelocity.magnitude:F1} m/s");

            // 엔진 정지
            if (_movement != null) _movement.SetEngineOn(false);

            // 이펙트
            if (crashEffect != null)
                Instantiate(crashEffect, position, Quaternion.identity);

            // 오디오 정지
            if (_audio != null) _audio.Stop();

            // 물리: 중력에 맡김 (추력 제거됨)
            _rb.linearDamping = 0.2f;

            // 파괴
            if (destroyOnCrash)
                Destroy(gameObject, 3f);

            // 상태 이벤트 (VehicleController가 구독)
            OnCrash?.Invoke();
        }

        // ══════════════════════════════════════════
        // 이벤트
        // ══════════════════════════════════════════
        public event System.Action OnCrash;

        // ══════════════════════════════════════════
        // 프로펠러 회전
        // ══════════════════════════════════════════
        private void SpinPropellers()
        {
            if (propellers == null || propellers.Length == 0) return;

            float thrustFactor = (_movement != null && _movement.MaxThrust > 0)
                ? Mathf.Clamp01(_movement.CurrentThrust / _movement.MaxThrust)
                : 0.1f;
            float rpm = _movement != null && _movement.EngineOn
                ? Mathf.Lerp(propellerIdleRPM, propellerMaxRPM, thrustFactor)
                : 0f;
            float degreesPerFrame = rpm / 60f * 360f * Time.deltaTime;

            for (int i = 0; i < propellers.Length; i++)
            {
                if (propellers[i] == null) continue;
                float dir = (i % 2 == 0) ? 1f : -1f;
                propellers[i].Rotate(Vector3.up, dir * degreesPerFrame, Space.Self);
            }
        }

        public void ResetCrash()
        {
            _isCrashed = false;
            if (_movement != null) _movement.SetEngineOn(true);
            _rb.linearDamping = 1.2f;
            Debug.Log("[DronePhysics] 리셋 완료");
        }

        // ══════════════════════════════════════════
        // 디버그 Gizmos
        // ══════════════════════════════════════════
        private void OnDrawGizmosSelected()
        {
            if (!enableWind) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position,
                _windDirection * (windBaseStrength + gustStrength));
        }
    }
}