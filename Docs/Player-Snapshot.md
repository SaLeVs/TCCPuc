# Player — snapshot pré-merge

**Estado congelado em:** commit `ca75effb` (branch `HudGameControl`), árvore de trabalho limpa.
**Objetivo:** ter a verdade documentada antes do merge, para conferir — ou restaurar — o que o
merge de YAML bagunçar.

Prefab: `Assets/Prefabs/Player/Player.prefab` — blob `60bc33fca84051578aa64db3bca71b262488a246`
Ragdoll: `Assets/Prefabs/Player/PlayerRagdoll.prefab` — blob `5ff38a3a4f28da9b7cab98a0ebd497d1ca688e84`

---

## 0. Como restaurar

O prefab íntegro está no git. Depois do merge, para comparar:

```bash
git diff ca75effb -- Assets/Prefabs/Player/Player.prefab
```

Para jogar fora a versão mergeada e voltar a esta:

```bash
git checkout ca75effb -- Assets/Prefabs/Player/Player.prefab
```

Para conferir se o arquivo é bit-a-bit o mesmo de antes do merge:

```bash
git hash-object Assets/Prefabs/Player/Player.prefab
```

Deve imprimir `60bc33fca84051578aa64db3bca71b262488a246`.

> **Antes de mergear**, crie uma âncora que não depende de lembrar o sha:
> `git tag player-pre-merge ca75effb`

### O que conferir primeiro depois do merge

Na ordem, porque é o que quebra em merge de prefab:

1. **Ordem dos componentes na raiz** (seção 3). O Netcode chama `OnNetworkSpawn` na ordem da lista.
   `PlayerState` **tem que vir antes** de todos que se inscrevem nele.
2. **Referências vazias** (seção 5). Um merge que perde um `fileID` deixa o campo em `None` e o
   erro só aparece em runtime. A única referência legitimamente vazia hoje é `NoiseEmitter.origin`.
3. **Componentes duplicados.** Merge de YAML adora duplicar blocos `--- !u!114`. Se aparecerem dois
   `PlayerMovement` na raiz, o movimento fica com força dobrada.
4. **Os 8 componentes no `GFX`** (seção 4). Vivem como `m_AddedComponents` numa instância do FBX,
   que é a estrutura mais frágil do arquivo inteiro.
5. **Layers e estado ativo** (seção 6).

---

## 1. Hierarquia

```
Player                          layer 11 (Player)
│   Rigidbody: massa 55, linear damping 0, angular damping 0.05, gravidade on,
│              interpolate on, collision detection ContinuousDynamic,
│              constraints 112 (trava rotação X+Y+Z)
│   CapsuleCollider: radius 0.36625275, height 1.3992002, direction 1 (Y),
│                    center {0, 0.72539985, 0.14625272}, material Player_Physic
│
├── Orientation                 transform puro; yaw escrito por PlayerCamera.LateUpdate
├── CameraRoot                  NetworkTransform (InLocalSpace = 1, authority Owner)
│   └── PlayerCamera            CinemachineCamera, HardLockToTarget, PanTilt,
│       │                       BasicMultiChannelPerlin, InputAxisController
│       └── Flashlight          Light, UniversalAdditionalLightData, Flashlight, NoiseEmitter
├── GFX  ▸ Player_Fix.fbx       + 8 componentes adicionados (seção 4)
├── RagdollSpawn                âncora de instanciação do PlayerRagdoll.prefab
├── ScreenCanvas                Canvas, CanvasScaler, GraphicRaycaster + 8 scripts de UI
│   ├── GameplayCanvas          CanvasGroup
│   │   ├── Crosshair
│   │   ├── InventoryHolder     HorizontalLayoutGroup, InventoryUi
│   │   ├── DonationHolder      DonationUiController, ContentSizeFitter, HudAnimationPanel
│   │   └── ▸ ChatBar  ▸ AudienceBar  ▸ LifeBar  ▸ StaminaBar
│   │       ▸ MissionHolder  ▸ REC  ▸ BatteryBar
│   ├── SpectatorCanvas  [OFF]  PlayerSpectatorUi
│   │   ├── ▸ CameraFilter
│   │   └── Spectator           PlayerName, NextPlayerButton, PreviousPlayerButton
│   ├── WinCanvas        [OFF]  Background, Panel/WinText, Button, 4x Film_*
│   ├── LoseCanvas       [OFF]  Background, Panel/LoseText, Button, 4x Film_*
│   ├── MissionUi        [OFF]  SkillCheckController, SkillCheckGenerator
│   │   └── MissionPanel/MissionObject/{SkillCheck/CorrectAreas, Pointer/CheckArea}, Check, Leave
│   └── PauseCanvas      [OFF]  PausePanel/{Background, LoseText, PlayersVision/…,
│                               ResumeButton, OptionsButton, QuitGame}, ▸ OptionsMenu
└── WorldSpaceCanvas            Canvas, CanvasScaler, PlayerInfosUi, LookAtCamera
    └── PlayerName              TextMeshProUGUI
```

`▸` = instância de prefab aninhado.

### Prefabs aninhados referenciados

| GameObject | Prefab de origem |
|---|---|
| GFX | `Player_Fix.fbx` |
| ChatBar | `ChatBar.prefab` |
| AudienceBar | `AudienceBar.prefab` |
| LifeBar | `LifeBar.prefab` |
| StaminaBar | `StaminaBar.prefab` |
| MissionHolder | `MissionHolder.prefab` |
| REC | `REC.prefab` |
| BatteryBar | `BatteryBar.prefab` |
| CameraFilter | `CameraFilter.prefab` |
| OptionsMenu | `OptionsMenu.prefab` |

---

## 2. Transforms-chave

| GameObject | localPosition | localScale |
|---|---|---|
| Player | `0, 0, 0` | `1, 1, 1` |
| Orientation | `0, 0, 0` | `1, 1, 1` |
| CameraRoot | `0, 1, 0.3` | `1, 1, 1` |
| PlayerCamera | `0, 0, 0` (rot `0.0784, -0.0153, 0.0012, 0.9968`) | `1, 1, 1` |
| Flashlight | `0, 0, 0` | `1, 1, 1` |
| RagdollSpawn | `0, 0, 0` | `1, 1, 1` |
| WorldSpaceCanvas | `0, 0, 0` | `0.01, 0.01, 0.01` |

`CameraRoot.localPosition` = `0, 1, 0.3` bate com `PlayerCameraOffset.standingOffset` = `0, 1, 0.35`.
Não são idênticos, mas o `standingOffset` sobrescreve no primeiro `LateUpdate` do owner.

---

## 3. Ordem dos componentes  ⚠️ confira isto primeiro

### Raiz `Player` — 28 componentes

```
 1. Transform               11. PlayerCameraOffset     21. PlayerInfos
 2. NetworkObject           12. PlayerAnimations       22. PlayerKnockdown
 3. NetworkTransform        13. PlayerInventory        23. RecordableIdentifier
 4. NetworkRigidbody        14. PlayerInteractor       24. AudienceContributor
 5. PlayerState             15. CameraVision           25. VisionSensor
 6. PlayerMovement          16. PlayerDead             26. PlayerNoiseProfile
 7. PlayerRun               17. PlayerDroppper         27. Rigidbody
 8. PlayerStairClimber      18. PlayerDiskHolder       28. CapsuleCollider
 9. PlayerCrouch            19. Health
10. PlayerCamera            20. PlayerMissionHolder
```

`PlayerState` em **5** é o que importa: ele é o hub de eventos e precisa ter rodado
`OnNetworkSpawn` antes de 6, 7, 9, 10, 11, 12, 13, 14, 22, 26 — todos se inscrevem nele.

### `ScreenCanvas` — 12 componentes

```
 1. RectTransform    4. GraphicRaycaster   7. GameOverScreenUi  10. SettingUiConfigs
 2. Canvas           5. PlayerSpectator    8. PauseUi           11. HudVisibilityController
 3. CanvasScaler     6. VictoryScreenUi    9. SoundMissionUi    12. PlayerListInGameUi
```

---

## 4. Componentes adicionados na instância `GFX`  ⚠️ estrutura frágil

Não estão no `Player_Fix.fbx`: são `m_AddedComponents` da instância, e é o que um merge ruim
apaga em silêncio. Na raiz do `GFX`, nesta ordem:

```
1. NetworkAnimator        5. PlayerZoneAudioState
2. FootstepEmitter        6. RemoteVoiceFilter
3. VivoxPlayer            7. DonationMicWatcher
4. PlayerVoiceIdentity    8. PlayerMicReporter
```

O `Animator` vem do próprio FBX. `PlayerAnimations.animator` e `PlayerDead.playerAnimator`
apontam para ele.

---

## 5. Wiring completo

Todo campo `[SerializeField]` de todo script do projeto, com os `fileID` já resolvidos para
nomes. É a lista de conferência campo a campo.

`<VAZIO>` = referência nula no prefab. **Hoje só existe uma:** `NoiseEmitter.origin` na
`Flashlight`. Qualquer outro `<VAZIO>` depois do merge é regressão.

### `AudienceContributor`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
visionSensor                     VisionSensor @ Player
```

### `CameraVision`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
visionSensor                     VisionSensor @ Player
```

### `Health`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
currentHealth:
    UpdateTraits:
    MinSecondsBetweenUpdates: 0
    MaxSecondsBetweenUpdates: 0
    m_InternalValue: 0
```

### `PlayerAnimations`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
animator                         Animator dentro da instancia de Player_Fix.fbx
animationSmoothing               0.1
```

### `PlayerCamera`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
cinemachineCamera                CinemachineCamera @ Player/CameraRoot/PlayerCamera
inputAxisController              CinemachineInputAxisController @ Player/CameraRoot/PlayerCamera
panTilt                          CinemachinePanTilt @ Player/CameraRoot/PlayerCamera
cameraRoot                       Transform @ Player/CameraRoot
inputReader                      asset -> InputReader.asset
orientation                      Transform @ Player/Orientation
rb                               Rigidbody @ Player
occlusionRenderers:
  -  SkinnedMeshRenderer dentro da instancia de Player_Fix.fbx
ownerCameraPriority              10
minSensitivityMultiplier         1
maxSensitivityMultiplier         20
```

### `PlayerCameraOffset`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
cinemachineCamera                CinemachineCamera @ Player/CameraRoot/PlayerCamera
cameraRoot                       Transform @ Player/CameraRoot
cameraMoveSpeed                  10
standingOffset                   {x: 0, y: 1, z: 0.35}
crouchOffset                     {x: 0, y: 0.7, z: 0.45}
runOffset                        {x: 0, y: 0.9, z: 0.5}
deadOffset                       {x: 0, y: 0, z: 0}
```

### `PlayerCrouch`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
inputReader                      asset -> InputReader.asset
capsuleCollider                  CapsuleCollider @ Player
speedModifier                    0.5
crouchColliderCenter             {x: 0.02667567, y: 0.57, z: 0.1080268}
crouchRadius                     0.3629974
crouchHeight                     1.156486
crouchSpeed                      10
ceilingCheck                     Transform @ CeilingCheck
ceilingCheckDistance             0.5
ceilingMask:
    serializedVersion: 2
    m_Bits: 1000576
```

### `PlayerDead`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerHealth                     Health @ Player
playerAnimator                   Animator dentro da instancia de Player_Fix.fbx
ragdollSpawnRoot                 Transform @ Player/RagdollSpawn
ragdollPrefab                    asset -> PlayerRagdoll.prefab
deadLayer:
    serializedVersion: 2
    m_Bits: 4096
aliveLayer:
    serializedVersion: 2
    m_Bits: 2048
```

### `PlayerDiskHolder`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
```

### `PlayerDroppper`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerInventory                  PlayerInventory @ Player
playerDiskHolder                 PlayerDiskHolder @ Player
playerDead                       PlayerDead @ Player
itemDatabase                     asset -> ItemDataBase.asset
diskItem                         asset -> Disket.prefab
```

### `PlayerInfos`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
PlayerName:
    UpdateTraits:
    MinSecondsBetweenUpdates: 0
    MaxSecondsBetweenUpdates: 0
    m_InternalValue:
    utf8LengthInBytes: 0
    bytes:
    offset0000:
    byte0000: 0
    byte0001: 0
    byte0002: 0
    byte0003: 0
    byte0004: 0
    byte0005: 0
    byte0006: 0
    byte0007: 0
    byte0008: 0
    byte0009: 0
    byte0010: 0
    byte0011: 0
    byte0012: 0
    byte0013: 0
    byte0014: 0
    byte0015: 0
    byte0016: 0
    byte0017: 0
    byte0018: 0
    byte0019: 0
    byte0020: 0
    byte0021: 0
    byte0022: 0
    byte0023: 0
    byte0024: 0
    byte0025: 0
    byte0026: 0
    byte0027: 0
    byte0028: 0
    byte0029: 0
```

### `PlayerInteractor`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
inputReader                      asset -> InputReader.asset
playerView                       Transform @ Player/CameraRoot
playerCamera                     PlayerCamera @ Player
rayOriginOverride                Transform @ Player/CameraRoot/PlayerCamera
interactDistance                 1
checkInterval                    0.2
layerMask:
    serializedVersion: 2
    m_Bits: 262400
```

### `PlayerInventory`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
maxInventorySize                 3
itemDatabase                     asset -> ItemDataBase.asset
inputReader                      asset -> InputReader.asset
playerHand                       Transform dentro da instancia de Player_Fix.fbx
```

### `PlayerKnockdown`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
playerDead                       PlayerDead @ Player
playerCameraOffset               PlayerCameraOffset @ Player
playerCamera                     PlayerCamera @ Player
playerHealth                     Health @ Player
playerAnimator                   Animator dentro da instancia de Player_Fix.fbx
playerRigidbody                  Rigidbody @ Player
playerCapsule                    CapsuleCollider @ Player
ragdollSpawnRoot                 Transform @ Player/RagdollSpawn
ragdollPrefab                    asset -> PlayerRagdoll.prefab
standingBlockingMask:
    serializedVersion: 2
    m_Bits: 1011584
searchMaxDistance                4
searchStepSize                   0.5
searchAngleStep                  20
groundOffset                     0
```

### `PlayerMissionHolder`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
_playerMissionHolderUi           PlayerMissionHolderUi dentro da instancia de MissionHolder.prefab
```

### `PlayerMovement`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
inputReader                      asset -> InputReader.asset
rb                               Rigidbody @ Player
orientation                      Transform @ Player/Orientation
moveSpeed                        3
blendMovementTime                8.9
acceleration                     20
deceleration                     30
```

### `PlayerNoiseProfile`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
footstepEmitter                  FootstepEmitter @ instancia de Player_Fix.fbx
playerCrouch                     PlayerCrouch @ Player
playerRun                        PlayerRun @ Player
crouchingMultiplier              0.3
walkingMultiplier                1
runningMultiplier                1.8
```

### `PlayerRun`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
inputReader                      asset -> InputReader.asset
speedModifier                    2
staminaMax                       5
staminaDrainPerSecond            1.25
staminaGainPerSecond             0.75
staminaCooldownThreshold         4
minMovementThreshold             0.1
```

### `PlayerStairClimber`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
rb                               Rigidbody @ Player
maxStepHeight                    0.3
stepReachDistance                0.5
stepLayerMask:
    serializedVersion: 2
    m_Bits: 128
stepClimbSpeed                   5
climbTimeout                     0.4
```

### `PlayerState`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
playerMovement                   PlayerMovement @ Player
playerRun                        PlayerRun @ Player
playerCrouch                     PlayerCrouch @ Player
playerInventory                  PlayerInventory @ Player
playerInteractor                 PlayerInteractor @ Player
playerDead                       PlayerDead @ Player
playerCamera                     PlayerCamera @ Player
playerCameraOffset               PlayerCameraOffset @ Player
playerInfos                      PlayerInfos @ Player
```

### `RecordableIdentifier`  em  `Player`
```
targetType                       1
minimumViewTime                  2
audienceGain                     50
reviewCooldown                   60
canBeReviewed                    1
canBeReviewedForChat             1
chatCooldown                     20
```

### `VisionSensor`  em  `Player`
```
ShowTopMostFoldoutHeaderGroup    1
orientation                      Transform @ Player/CameraRoot/PlayerCamera
distance                         10
angle                            70
height                           1.3
closeProximityRadius             2
meshColor                        {r: 0.9811321, g: 0.06016379, b: 0.06016379, a: 0.83137256}
scanFrequency                    5
targetLayers:
    serializedVersion: 2
    m_Bits: 25344
occlusionLayers:
    serializedVersion: 2
    m_Bits: 984448
detectedObjects                  []
previousDetectedObjects          []
```

### `DonationMicWatcher`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
ShowTopMostFoldoutHeaderGroup    1
micActionId                      default
```

### `FootstepEmitter`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
ShowTopMostFoldoutHeaderGroup    1
source                           0
footstepLoudness                 9
reportsNoise                     1
```

### `HudAnimationPanel`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
```

### `HudAnimationPanel`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
```

### `PlayerMicReporter`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
ShowTopMostFoldoutHeaderGroup    1
micWatcherBehaviour              DonationMicWatcher @ instancia de Player_Fix.fbx
audioEnergyThreshold             0.65
voiceMakesNoise                  1
speechEnergyThreshold            0.25
shoutEnergyThreshold             0.65
speechLoudness                   10
shoutLoudness                    24
voiceReportInterval              0.35
```

### `PlayerMissionHolderUi`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
```

### `PlayerVoiceIdentity`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
ShowTopMostFoldoutHeaderGroup    1
```

### `PlayerZoneAudioState`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
```

### `RemoteVoiceFilter`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
voiceIdentity                    PlayerVoiceIdentity @ instancia de Player_Fix.fbx
zoneState                        PlayerZoneAudioState @ instancia de Player_Fix.fbx
rangeScalesWithVoice             1
speechEnergyThreshold            0.25
shoutEnergyThreshold             0.65
whisperDistance                  8
rangeAttackSeconds               0.08
rangeReleaseSeconds              0.9
```

### `VivoxPlayer`  em  `Player/GFX  (adicionado na instancia do FBX)`
```
ShowTopMostFoldoutHeaderGroup    1
earTransform                     Transform dentro da instancia de Player_Fix.fbx
```

### `GameOverScreenUi`  em  `Player/ScreenCanvas`
```
playerState                      PlayerState @ Player
gameOverPanel                    GameObject -> Player/ScreenCanvas/LoseCanvas
mainMenuSceneName                MainMenu
gameOverDelay                    4
```

### `HudVisibilityController`  em  `Player/ScreenCanvas`
```
ShowTopMostFoldoutHeaderGroup    1
inputReader                      asset -> InputReader.asset
bindings:
    - action: 0
    panel HudAnimationPanel dentro da instancia de ChatBar.prefab
    - action: 1
    panel HudAnimationPanel dentro da instancia de MissionHolder.prefab
donations                        DonationUiController @ Player/ScreenCanvas/GameplayCanvas/DonationHolder
playerState                      PlayerState @ Player
gameplayCanvas                   GameObject -> Player/ScreenCanvas/GameplayCanvas
playerCamera                     PlayerCamera @ Player
```

### `PauseUi`  em  `Player/ScreenCanvas`
```
pausePanel                       GameObject -> Player/ScreenCanvas/PauseCanvas
optionPanel                      GameObject dentro da instancia de OptionsMenu.prefab
mainMenuSceneName                MainMenu
playerCamera                     PlayerCamera @ Player
```

### `PlayerListInGameUi`  em  `Player/ScreenCanvas`
```
ShowTopMostFoldoutHeaderGroup    1
playerListContent                RectTransform @ Player/ScreenCanvas/PauseCanvas/PausePanel/PlayersVision/PlayerVisionPanel/PlayersPanel
playerListItemUi                 asset -> PlayerListItemUi.prefab
includeSelfInList                1
playerCamera                     PlayerCamera @ Player
```

### `PlayerSpectator`  em  `Player/ScreenCanvas`
```
ShowTopMostFoldoutHeaderGroup    1
playerState                      PlayerState @ Player
gameplayCanvas                   GameObject -> Player/ScreenCanvas/GameplayCanvas
spectatorCanvas                  GameObject -> Player/ScreenCanvas/SpectatorCanvas
timeToEnterInSpectator           6
```

### `SettingUiConfigs`  em  `Player/ScreenCanvas`
```
musicMixer                       asset -> MusicSoundMixer.mixer
sfxMixer                         asset -> SFXSoundMixer.mixer
```

### `SoundMissionUi`  em  `Player/ScreenCanvas`
```
missionPanel                     GameObject -> Player/ScreenCanvas/MissionUi
skillCheckController             SkillCheckController @ Player/ScreenCanvas/MissionUi
```

### `VictoryScreenUi`  em  `Player/ScreenCanvas`
```
playerState                      PlayerState @ Player
victoryPanel                     GameObject -> Player/ScreenCanvas/WinCanvas
mainMenuSceneName                MainMenu
```

### `LookAtCamera`  em  `Player/WorldSpaceCanvas`
```
mode                             1
canvasTransform                  RectTransform @ Player/WorldSpaceCanvas
```

### `PlayerInfosUi`  em  `Player/WorldSpaceCanvas`
```
canvas                           Canvas @ Player/WorldSpaceCanvas
playerState                      PlayerState @ Player
playerNameText                   TextMeshProUGUI @ Player/WorldSpaceCanvas/PlayerName
```

### `SkillCheckController`  em  `Player/ScreenCanvas/MissionUi`
```
checkArea                        RectTransform @ Player/ScreenCanvas/MissionUi/MissionPanel/MissionObject/Pointer/CheckArea
generator                        SkillCheckGenerator @ Player/ScreenCanvas/MissionUi
successDistance                  30
requiredCorrectChecks            4
```

### `SkillCheckGenerator`  em  `Player/ScreenCanvas/MissionUi`
```
slotPrefab                       asset -> CorrectArea.prefab
slotsParent                      RectTransform @ Player/ScreenCanvas/MissionUi/MissionPanel/MissionObject/SkillCheck/CorrectAreas
correctAreasCount                4
radius                           82
minAngleGapDegrees               20
rotationOffset                   -20
```

### `PlayerSpectatorUi`  em  `Player/ScreenCanvas/SpectatorCanvas`
```
playerSpectatorController        PlayerSpectator @ Player/ScreenCanvas
playerNameText                   TextMeshProUGUI @ Player/ScreenCanvas/SpectatorCanvas/Spectator/PlayerName
```

### `Flashlight`  em  `Player/CameraRoot/PlayerCamera/Flashlight`
```
ShowTopMostFoldoutHeaderGroup    1
inputReader                      asset -> InputReader.asset
flashlight                       Light @ Player/CameraRoot/PlayerCamera/Flashlight
playerState                      PlayerState @ Player
clickNoise                       NoiseEmitter @ Player/CameraRoot/PlayerCamera/Flashlight
batteryPercentMax                400
batteryPercentDecreasePerSecond  4
```

### `NoiseEmitter`  em  `Player/CameraRoot/PlayerCamera/Flashlight`
```
type                             2
loudness                         4
cooldown                         0.2
origin                           <VAZIO>
```

### `DonationUiController`  em  `Player/ScreenCanvas/GameplayCanvas/DonationHolder`
```
cardContainer                    RectTransform @ Player/ScreenCanvas/GameplayCanvas/DonationHolder
cardPrefab                       asset -> DonationFeed.prefab
trayContainer                    RectTransform dentro da instancia de MissionHolder.prefab
chipPrefab                       asset -> DonationChip.prefab
trayRoot                         GameObject dentro da instancia de MissionHolder.prefab
alertBaseSeconds                 1.5
alertWordsPerSecond              1.25
alertSecondsRange                {x: 3, y: 12}
onCardShown                      UnityEvent: HudAnimationPanel @ Player/ScreenCanvas/GameplayCanvas/DonationHolder -> Show()
onCardHidden                     UnityEvent: HudAnimationPanel @ Player/ScreenCanvas/GameplayCanvas/DonationHolder -> Hide()
```

### `HudAnimationPanel`  em  `Player/ScreenCanvas/GameplayCanvas/DonationHolder`
```
moves:
  - target RectTransform @ Player/ScreenCanvas/GameplayCanvas/DonationHolder
    left: 0
    right: 0
    bottom: 400
    top: 400
    duration: 0.25
    curve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
    time: 0
    value: 0
    inSlope: 0
    outSlope: 0
    tangentMode: 0
    weightedMode: 0
    inWeight: 0
    outWeight: 0
    - serializedVersion: 3
    time: 1
    value: 1
    inSlope: 0
    outSlope: 0
    tangentMode: 0
    weightedMode: 0
    inWeight: 0
    outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
    delay: 0
    authoredShownMin: {x: 0, y: 0}
    authoredShownMax: {x: 0, y: 0}
  - target RectTransform dentro da instancia de MissionHolder.prefab
    left: 0
    right: 0
    bottom: 480
    top: 215
    duration: 0.25
    curve:
    serializedVersion: 2
    m_Curve:
    - serializedVersion: 3
    time: 0
    value: 0
    inSlope: 0
    outSlope: 0
    tangentMode: 0
    weightedMode: 0
    inWeight: 0
    outWeight: 0
    - serializedVersion: 3
    time: 1
    value: 1
    inSlope: 0
    outSlope: 0
    tangentMode: 0
    weightedMode: 0
    inWeight: 0
    outWeight: 0
    m_PreInfinity: 2
    m_PostInfinity: 2
    m_RotationOrder: 4
    delay: 0
    authoredShownMin: {x: 0, y: 0}
    authoredShownMax: {x: 0, y: 0}
hideWhenHidden                   []
showWhenHidden                   []
visibleOnStart                   0
onVisibilityChanged              UnityEvent (vazio)
hasAuthoredShown                 0
```

### `InventoryUi`  em  `Player/ScreenCanvas/GameplayCanvas/InventoryHolder`
```
ShowTopMostFoldoutHeaderGroup    1
inventory                        PlayerInventory @ Player
itemDatabase                     asset -> ItemDataBase.asset
inventoryHolder                  RectTransform @ Player/ScreenCanvas/GameplayCanvas/InventoryHolder
inventorySlotPrefab              asset -> Slot.prefab
```

### `InfiniteCassetteMovement`  em  `Player/ScreenCanvas/LoseCanvas/Film_Bottom`
```
speed                            30
secondImage                      RectTransform @ Player/ScreenCanvas/LoseCanvas/Film_Bottom_Fundo
overlapOffset                    135.06
```

### `InfiniteCassetteMovement`  em  `Player/ScreenCanvas/LoseCanvas/Film_Top`
```
speed                            30
secondImage                      RectTransform @ Player/ScreenCanvas/LoseCanvas/Film_Top_Fundo
overlapOffset                    135.06
```

### `InfiniteCassetteMovement`  em  `Player/ScreenCanvas/WinCanvas/Film_Bottom`
```
speed                            30
secondImage                      RectTransform @ Player/ScreenCanvas/WinCanvas/Film_Bottom_Fundo
overlapOffset                    135.06
```

### `InfiniteCassetteMovement`  em  `Player/ScreenCanvas/WinCanvas/Film_Top`
```
speed                            30
secondImage                      RectTransform @ Player/ScreenCanvas/WinCanvas/Film_Top_Fundo
overlapOffset                    135.06
```

### `CircularPointer`  em  `Player/ScreenCanvas/MissionUi/MissionPanel/MissionObject/Pointer`
```
pointer                          RectTransform @ Player/ScreenCanvas/MissionUi/MissionPanel/MissionObject/Pointer
radius                           90
rotationSpeed                    180
```

---

## 6. Layers e estado ativo

Tudo em layer **11 (Player)** e ativo, exceto:

| GameObject | Layer | Ativo |
|---|---|---|
| ScreenCanvas/SpectatorCanvas | 11 | **não** |
| ScreenCanvas/WinCanvas | 11 | **não** |
| ScreenCanvas/LoseCanvas | 11 | **não** |
| ScreenCanvas/MissionUi | 11 | **não** |
| ScreenCanvas/PauseCanvas | 11 | **não** |
| WorldSpaceCanvas | 5 (UI) | sim |
| WorldSpaceCanvas/PlayerName | 5 (UI) | sim |
| Win/LoseCanvas/Film_Top, Film_Top_Fundo, Film_Bottom, Film_Bottom_Fundo | 5 (UI) | sim |

`PlayerDead.deadLayer` = bits 4096 = layer **12 (DeadPlayer)**
`PlayerDead.aliveLayer` = bits 2048 = layer **11 (Player)**

---

## 7. Configuração de rede

| Componente | Onde | Configuração |
|---|---|---|
| `NetworkObject` | Player | — |
| `NetworkTransform` | Player | `InLocalSpace = 0` (world), authority **Owner**, `TickSyncChildren = 1`, sincroniza posição XYZ, **não** sincroniza escala |
| `NetworkRigidbody` | Player | — |
| `NetworkTransform` | CameraRoot | `InLocalSpace = 1` (local), authority **Owner**, `TickSyncChildren = 0`, sincroniza posição XYZ, **não** sincroniza escala |
| `NetworkAnimator` | GFX | — |

---

## 8. Scripts do assembly `Player`

Assembly `Player.asmdef`, 14 referências. Hashes para detectar mudança de código no merge:

| Arquivo | blob (12) | Linhas |
|---|---|---|
| PlayerState.cs | `c7a602a3b14a` | 221 |
| PlayerMovement.cs | `af3a7b195e62` | 175 |
| PlayerRun.cs | `067b58bcbc40` | 134 |
| PlayerCrouch.cs | `e882be69d8b3` | 183 |
| PlayerStairClimber.cs | `ba133a893f69` | 126 |
| PlayerCamera.cs | `5ad784af30e7` | 209 |
| PlayerCameraOffset.cs | `7bcaf466b363` | 186 |
| PlayerCameraStack.cs | `2a712caccc94` | 68 |
| PlayerAnimations.cs | `8838c9d2b0a7` | 127 |
| PlayerInventory.cs | `42570c76b91e` | 324 |
| PlayerInteractor.cs | `57b831b45e68` | 209 |
| PlayerDroppper.cs | `2e23db34cca6` | 94 |
| PlayerDead.cs | `fdb7b584f749` | 156 |
| PlayerKnockdown.cs | `61fc7d288557` | 370 |
| PlayerRagdoll.cs | `7bfcf48bb2bc` | 73 |
| StandingSpotFinder.cs | `74b55a7ccc86` | 77 |
| PlayerSpectator.cs | `9b5a3d1f24ea` | 203 |
| PlayerNoiseProfile.cs | `61811dca8c14` | 67 |
| PlayerMicReporter.cs | `1165b7bf40f3` | 201 |
| PlayerInfos.cs | `309950d9d091` | 24 |
| VivoxPlayer.cs | `56b3611e0169` | 32 |
| Chat/ChatManager.cs | `9d137ced68f0` | 506 |
| Chat/ChatDirector.cs | `7db57560bfdd` | 448 |
| Chat/ChatUi.cs | `600d23ce7cbe` | 173 |

Os três de `Chat/` vivem no `ChatBar.prefab`, não neste prefab.

---

## 9. Arquitetura, em uma página

`PlayerState` é um **hub de eventos**, não um controlador. Os sistemas de baixo nível publicam
nele; os consumidores se inscrevem só nele. Ninguém faz `GetComponent` em runtime — tudo é
`[SerializeField]` amarrado no prefab.

```
  InputReader.asset (ScriptableObject)
        │
        ├──► PlayerMovement ──┐
        ├──► PlayerRun ───────┤
        ├──► PlayerCrouch ────┤  publicam
        ├──► PlayerInteractor ┤
        ├──► PlayerInventory ─┤
        └──► PlayerCamera     │
                              ▼
   Health ──► PlayerDead ──► PlayerState  ◄── PlayerKnockdown
                              │  (hub)
                              │  OnPlayerMovement / OnPlayerMovementInput / OnRunEvent /
                              │  OnCrouchEvent / OnInteract / OnHoldItem / OnPlayerDead /
                              │  OnPlayerLocked / OnPlayerWon / OnVictoryTriggered /
                              │  OnGameOverTriggered
                              ▼
        ┌─────────────┬──────────────┬─────────────────┬──────────────┐
  PlayerAnimations  PlayerCameraOffset  PlayerNoiseProfile  PlayerStairClimber  PlayerSpectator
```

**Velocidade** é composta: `PlayerMovement.Awake` faz `GetComponents<ISpeedModifier>()` uma vez.
Implementam hoje `PlayerRun` (x2) e `PlayerCrouch` (x0.5). Adicionar um modificador novo não
toca em `PlayerMovement`.

**Autoridade:** input e câmera no owner; dano, morte e inventário no servidor;
`PlayerMicReporter` manda só a intensidade do som — a posição vem da cópia do servidor.

**Ciclo de vida:** todo `OnNetworkSpawn` tem seu `OnNetworkDespawn` espelhado, com duas
exceções conhecidas (seção 10).

---

## 10. Problemas conhecidos neste snapshot

Estavam aqui **antes** do merge. Se aparecerem depois, não foi o merge que causou.

| # | Onde | O quê |
|---|---|---|
| 1 | `CameraRoot` | `NetworkTransform` com `InLocalSpace = 1` sendo reparentado em runtime por `PlayerCameraOffset.AttachRagdollCamera` / `AttachCameraTo`. O owner passa a replicar posição em espaço de osso (Armature 100x) enquanto os remotos ainda têm o `CameraRoot` sob o `Player`. Sintoma: a lanterna dos outros salta para perto da origem do corpo durante knockdown/morte. |
| 2 | `PlayerDead.cs:29` | Não trata late joiner. `OnNetworkSpawn` só chama `ApplyLayerState`; o `OnValueChanged` não dispara para quem chega depois. Quem entra com alguém já morto vê o corpo em pé, sem ragdoll. `PlayerKnockdown` trata; `PlayerDead` não. |
| 3 | `PlayerCrouch.cs:172` | `OnNetworkDespawn` não desinscreve `playerState.OnPlayerLocked`. |
| 4 | `PlayerInventory.cs:302` | Idem: inscreve `OnPlayerLocked` e não desinscreve. |
| 5 | `PlayerSpectator.cs:186` | `OnNetworkDespawn` não desinscreve `OnPlayerDead` / `OnPlayerWon` dos players observados — vaza handlers nos outros `PlayerState`. |
| 6 | Prefab | O HUD inteiro vive dentro do prefab de player. Cada player remoto instancia o `ScreenCanvas` completo; `PlayerSpectator` desliga só `GameplayCanvas` e `SpectatorCanvas`. Numa sala de 4: 4 Canvas overlay e 4 `GraphicRaycaster` ativos. |
| 7 | 3 arquivos | `PlayerDead.DisableLivingBody`, `PlayerKnockdown.HideLivingBody` e `PlayerState.HidePlayerRpc` varrem `GetComponentsInChildren<Renderer/Collider>` com regras diferentes. Só o `PlayerKnockdown` registra o que desligou antes de restaurar. |
| 8 | `PlayerDead.cs:68` | `SetLayerRecursively` no spawn converte os 10 objetos autorados em layer UI (5) para Player (11), incluindo o `WorldSpaceCanvas`. |
| 9 | `PlayerCameraStack.cs` | Código morto: não está em nenhum prefab nem cena, e o GameObject `PlayerCamera` não tem componente `Camera`. |
| 10 | `Flashlight` | `NoiseEmitter.origin` vazio. |
| 11 | `PlayerDroppper.cs` | Arquivo com três `p`; a classe é `PlayerDropper`. Também tem `using Player;` dentro de `namespace Player`. |
| 12 | `FootstepEmitter` | XML doc de `LoudnessMultiplier` aponta para `Perception.PlayerNoiseProfile`; a classe real é `Player.PlayerNoiseProfile`. |

`StandingSpotFinder` não aparecer em prefab nenhum é esperado — é helper estático usado por
`PlayerKnockdown`.
