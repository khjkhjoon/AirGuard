using UnityEngine;
using System;

namespace AirGuard.Controllers
{
    /// <summary>
    /// 배터리 시스템
    /// </summary>
    public class BatterySystem : MonoBehaviour
    {
        [Header("Battery Settings")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float currentBattery = 100f;
        [SerializeField] private float drainRateIdle = 0.1f;      // 대기 시 소모율
        [SerializeField] private float drainRateActive = 0.5f;    // 활동 시 소모율
        [SerializeField] private float chargeRate = 10f;          // 충전 속도
        [SerializeField] private float criticalLevel = 20f;       // 위험 수준

        public float CurrentBattery => currentBattery;
        public float BatteryPercentage => (currentBattery / maxBattery) * 100f;
        public bool IsCritical => currentBattery < criticalLevel;
        public bool IsEmpty => currentBattery <= 0;

        public event Action OnBatteryCritical;
        public event Action OnBatteryEmpty;

        private bool _wasCritical;

        private void Start()
        {
            currentBattery = maxBattery;
        }

        public void Drain(bool isMoving)
        {
            if (currentBattery <= 0)
                return;

            float drainRate = isMoving ? drainRateActive : drainRateIdle;
            currentBattery = Mathf.Max(0, currentBattery - drainRate * Time.deltaTime);

            // 배터리 위험 수준 체크
            if (IsCritical && !_wasCritical)
            {
                OnBatteryCritical?.Invoke();
                _wasCritical = true;
            }

            // 배터리 소진 체크
            if (IsEmpty)
            {
                OnBatteryEmpty?.Invoke();
            }
        }

        public void Charge()
        {
            currentBattery = Mathf.Min(maxBattery, currentBattery + chargeRate * Time.deltaTime);

            if (currentBattery > criticalLevel)
            {
                _wasCritical = false;
            }
        }

        public void SetBattery(float amount)
        {
            currentBattery = Mathf.Clamp(amount, 0, maxBattery);
        }

        public void FullCharge()
        {
            currentBattery = maxBattery;
            _wasCritical = false;
        }
    }
}