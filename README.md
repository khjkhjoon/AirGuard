# 🛡️ AirGuard

> **실시간 드론 모니터링 시스템**  
> WPF 기반 시각화 모니터링 클라이언트 · 백엔드 서버 · 드론 시뮬레이터로 구성된 통합 플랫폼

---

## 📌 프로젝트 개요

AirGuard는 드론 환경 데이터를 실시간으로 수집·처리하고, WPF 기반의 모니터링 클라이언트로 시각화하는 시스템입니다. 서버가 데이터를 중계하고, Unity 드론 시뮬레이터가 테스트용 데이터를 생성하는 3-tier 아키텍처로 구성되어 있습니다.

---

## 🗂️ 프로젝트 구조

```
AirGuard/
├── AirGuard_Monitor/     # WPF 기반 실시간 모니터링 클라이언트
├── Server/               # 데이터 중계 및 처리 백엔드 서버
└── Vehiclesimulator/     # 드론 시뮬레이터 (Unity)
```

### AirGuard_Monitor
WPF로 제작된 실시간 모니터링 뷰어입니다. 기본적인 2D 화면과 Helix Toolkit을 이용한 3D 화면, OxyPlot 라이브러리를 활용한 실시간 텔레메트리 그래프(배터리, 속도, 고도) 시각화 기능을 제공합니다.

### Server
모니터링 클라이언트와 시뮬레이터 사이에서 데이터를 수신·처리·전달하는 백엔드 서버입니다.

### Vehiclesimulator
Unity를 사용하여 드론 환경 데이터를 시뮬레이션하고 서버로 전송합니다. 실제 하드웨어 없이 시스템을 테스트할 수 있습니다.

---

## 🛠️ 기술 스택

| 영역 | 기술 |
|------|------|
| 모니터링 클라이언트 | C#, WPF, OxyPlot, Helix Toolkit |
| 백엔드 서버 | C# |
| 시뮬레이터 | Unity |

---

## 🚀 시작하기

### 요구 사항

- [.NET 8.0 이상](https://dotnet.microsoft.com/) (AirGuard_Monitor, Server 실행에 필요)
- [Unity](https://unity.com/) (Vehiclesimulator 실행에 필요)


---

## ⚙️ 시스템 아키텍처

```
[Vehiclesimulator] ──→ [Server] ──→ [AirGuard_Monitor]
   드론 데이터 생성      데이터 중계      실시간 시각화
```

---


## 👤 개발자

- **khjkhjoon** — [GitHub](https://github.com/khjkhjoon)
