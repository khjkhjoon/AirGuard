using UnityEngine;
using System;
using AirGuard.Data;

namespace AirGuard.Controllers
{
    /// <summary>
    /// 차량 상태 관리
    /// </summary>
    public class VehicleStatusManager : MonoBehaviour
    {
        [Header("Current Status")]
        [SerializeField] private VehicleStatus currentStatus = VehicleStatus.Idle;

        public VehicleStatus CurrentStatus => currentStatus;

        public event Action<VehicleStatus> OnStatusChanged;

        private void Update()
        {
            HandleStatusInput();
        }

        private void HandleStatusInput()
        {
            // E: 긴급 상황
            if (Input.GetKeyDown(KeyCode.E))
            {
                SetStatus(VehicleStatus.Emergency);
            }

            // R: 정상 복구
            if (Input.GetKeyDown(KeyCode.R))
            {
                SetStatus(VehicleStatus.Active);
            }

            // M: 임무 모드
            if (Input.GetKeyDown(KeyCode.M))
            {
                if (currentStatus == VehicleStatus.InMission)
                    SetStatus(VehicleStatus.Active);
                else
                    SetStatus(VehicleStatus.InMission);
            }

            // H: 귀환 모드
            if (Input.GetKeyDown(KeyCode.H))
            {
                SetStatus(VehicleStatus.Returning);
            }
        }

        public void SetStatus(VehicleStatus newStatus)
        {
            if (currentStatus == newStatus)
                return;

            VehicleStatus oldStatus = currentStatus;
            currentStatus = newStatus;

            Debug.Log($"Status changed: {oldStatus} → {newStatus}");
            OnStatusChanged?.Invoke(newStatus);
        }

        public void AutoUpdateStatus(bool isMoving, bool isBatteryCritical)
        {
            // 배터리 위험 시 자동으로 긴급 상태
            if (isBatteryCritical && currentStatus != VehicleStatus.Emergency)
            {
                SetStatus(VehicleStatus.Emergency);
                return;
            }

            // 긴급 상태가 아니고 수동 설정도 아닌 경우
            if (currentStatus != VehicleStatus.Emergency &&
                currentStatus != VehicleStatus.InMission &&
                currentStatus != VehicleStatus.Returning)
            {
                if (isMoving && currentStatus != VehicleStatus.Active)
                {
                    SetStatus(VehicleStatus.Active);
                }
                else if (!isMoving && currentStatus == VehicleStatus.Active)
                {
                    SetStatus(VehicleStatus.Idle);
                }
            }
        }

        public Color GetStatusColor()
        {
            return currentStatus switch
            {
                VehicleStatus.Idle => Color.gray,
                VehicleStatus.Active => Color.green,
                VehicleStatus.InMission => Color.blue,
                VehicleStatus.Returning => new Color(1f, 0.5f, 0f), // Orange
                VehicleStatus.Emergency => Color.red,
                VehicleStatus.Offline => Color.black,
                VehicleStatus.Maintenance => Color.yellow,
                _ => Color.white
            };
        }
    }
}