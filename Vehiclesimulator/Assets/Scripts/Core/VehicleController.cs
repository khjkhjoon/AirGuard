using UnityEngine;
using AirGuard.Data;
using AirGuard.Controllers;
using AirGuard.Network;
using AirGuard.UI;
using System;

namespace AirGuard.Core
{
    /// <summary>
    /// 모든 컴포넌트 통합 관리
    /// </summary>
    /// 
    [RequireComponent(typeof(VehicleMovementController))]
    [RequireComponent(typeof(BatterySystem))]
    [RequireComponent(typeof(VehicleStatusManager))]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(MapDataSender))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Vehicle Information")]
        [SerializeField] private string vehicleId = "DRONE_001";
        [SerializeField] private string vehicleName = "드론 1호";
        [SerializeField] private VehicleType vehicleType = VehicleType.Drone;

        private VehicleMovementController _movementController;
        private BatterySystem _batterySystem;
        private VehicleStatusManager _statusManager;
        private NetworkManager _networkManager;
        private VehicleUIController _uiController;

        private VehicleData _vehicleData;

        private void Awake()
        {
            InitializeComponents();
            InitializeData();
        }
        private void Start()
        {
            SetupEventListeners();  
        }

        private void Update()
        {
            UpdateSystems();
            SendDataToServer();
            HandleChargeInput();
        }

        private void InitializeComponents()
        {
            _movementController = GetComponent<VehicleMovementController>();
            _batterySystem = GetComponent<BatterySystem>();
            _statusManager = GetComponent<VehicleStatusManager>();
            _networkManager = GetComponent<NetworkManager>();
            _uiController = GetComponent<VehicleUIController>();
        }

        private void InitializeData()
        {
            _vehicleData = new VehicleData(vehicleId, vehicleName);
        }

        private void SetupEventListeners()
        {
            _batterySystem.OnBatteryCritical += OnBatteryCritical;
            _batterySystem.OnBatteryEmpty += OnBatteryEmpty;

            _statusManager.OnStatusChanged += OnStatusChanged;
        }    

        private void UpdateSystems()
        {
            _batterySystem.Drain(_movementController.IsMoving);

            _statusManager.AutoUpdateStatus(
                _movementController.IsMoving,
                _batterySystem.IsCritical
                );

            if(_uiController != null)
            {
                _uiController.UpdateUI(
                    _vehicleData,
                    _batterySystem.BatteryPercentage,
                    _statusManager.CurrentStatus,
                    _networkManager.IsConnected,
                    _networkManager.ConnectionStatus
                    );
            }
        }   
        private void SendDataToServer()
        {
            _vehicleData.UpdateFromTransform(
                transform,
                _movementController.CurrentSpeed,
                _batterySystem.BatteryPercentage,
                _statusManager.CurrentStatus
                );

            _networkManager.SendVehicleData(_vehicleData);
        }

        private void HandleChargeInput()
        {
            if (Input.GetKey(KeyCode.B))
            {
                _batterySystem.Charge();
            }
        }

        #region Event Handlers

        private void OnBatteryCritical()
        {
            Debug.LogWarning($"[{vehicleName}] Battery Critical!");
            _statusManager.SetStatus(VehicleStatus.Emergency);
        }

        private void OnBatteryEmpty()
        {
            Debug.LogError($" [{vehicleName}] Battery Empty!");
            _statusManager.SetStatus(VehicleStatus.Offline);
        }

        private void OnStatusChanged(VehicleStatus newStatus)
        {
            Debug.Log($" [{vehicleName}] Status: {newStatus}");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if(_statusManager != null)
            {
                Gizmos.color = _statusManager.GetStatusColor();
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                Gizmos.DrawRay(transform.position, transform.forward * 2f);
            }

            if (_batterySystem != null)
            {
                Color batteryColor = _batterySystem.BatteryPercentage > 50 ? Color.green :
                                     _batterySystem.BatteryPercentage > 20 ? Color.yellow : Color.red;

                Gizmos.color = batteryColor;
                float barWidth = _batterySystem.BatteryPercentage / 100f;
                Gizmos.DrawCube(
                    transform.position + Vector3.up * 1.5f,
                    new Vector3(barWidth, 0.1f, 0.1f)
                );
            }
        }
        #endregion

        #region Public Methods

        public void SetVehicleInfo(string id, string name)
        {
            vehicleId = id;
            vehicleName = name;
            _vehicleData = new VehicleData(id, name);
        }

        public string GetVehicleId() => vehicleId;
        public string GetVehicleName() => vehicleName;
        public VehicleStatus GetStatus() => _statusManager.CurrentStatus;
        public float GetBattery() => _batterySystem.BatteryPercentage;
        public bool IsNetworkConnected() => _networkManager.IsConnected;

        #endregion
    }
}