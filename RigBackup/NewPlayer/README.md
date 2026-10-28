# Backup do rig — NewPlayer/Player.fbx

Salvo em 2026-10-22, antes de apagar e reimportar `Assets/Models/Player/NewPlayer/Player.fbx`.

## Arquivos

| Arquivo | O que é |
|---|---|
| `Player.fbx.meta.atual` | `.meta` do commit `0c4c7a05` (estado atual). Tem o corte novo do `Run_Forward` (12–35), mas o esqueleto veio do export quebrado (DEF_SPINE filho do ROOT). |
| `Player.fbx.meta.5665ed0d` | Último `.meta` que funcionava, com o FBX de 40 MB e o esqueleto completo (ORG_/MCH_/DEF_). |

## Identidade do asset (o que mantém as referências)

- **GUID:** `c1d2c1fd13a679646b6e7fdbf448e6aa`
- Referenciado por:
  - `Assets/Prefabs/Player/Player.prefab`: modelo, ossos, HandHolder/EarAnchor
  - `Assets/Prefabs/Player/PlayerRagdoll.prefab`: modelo e ossos do ragdoll
  - `Assets/Animations/Player/Controllers/PlayerAnimator.controller`: os 26 clips, pelo `internalID`
- Se o `.meta` for apagado, o Unity gera um GUID novo e essas três referências quebram. Para recuperar: depois do reimport, troque a linha `guid:` do `.meta` novo por este GUID, devolva os `internalID` dos clips (tabela abaixo) e reimporte.
- Os ossos são referenciados por fileIDs calculados a partir do caminho na hierarquia. Se o caminho de um `DEF_` mudar (por exemplo, `DEF_SPINE` sair de baixo do `DEF_HIPS`), os objetos filhos (HandHolder em DEF_HAND.R, EarAnchor/DeadParent/VoiceAnchor em DEF_HEAD) e o ragdoll perdem o vínculo.

## Importer

- Rig: **Humanoid** (`animationType: 3`), Avatar **Create From This Model**, sem root motion (`rootMotionBoneName` vazio). O Player usa `Apply Root Motion = off`.
- Avatar: armTwist 0.5, foreArmTwist 0.5, upperLegTwist 0.5, legTwist 0.5, armStretch 0.05, legStretch 0.05, feetSpacing 0, Translation DoF off.
- Pose do esqueleto: braços forçados em T-pose (Enforce T-Pose) no skeleton do avatar.
- Animação: Anim. Compression = Keyframe Reduction (0.5 / 0.5 / 0.5), Resample Curves on.
- Model: Scale Factor 1, Use File Scale on, Read/Write off, Blend Shapes on. Material: M_Player (Material Import Mode = Import via MaterialDescription; o prefab usa M_Player).

## Mapa Humanoid

| Humano | Osso |
|---|---|
| Hips | `DEF_HIPS` |
| Spine | `DEF_SPINE` |
| Chest | `DEF_CHEST` |
| Neck | `DEF_NECK` |
| Head | `DEF_HEAD` |
| LeftUpperLeg | `DEF_LEG.L` |
| LeftLowerLeg | `DEF_SHIN.L` |
| LeftFoot | `DEF_FOOT.L` |
| LeftToes | `DEF_TOES.L` |
| RightUpperLeg | `DEF_LEG.R` |
| RightLowerLeg | `DEF_SHIN.R` |
| RightFoot | `DEF_FOOT.R` |
| RightToes | `DEF_TOES.R` |
| LeftShoulder | `DEF_SHOULDER.L` |
| LeftUpperArm | `DEF_ARM.L` |
| LeftLowerArm | `DEF_FOREARM.L` |
| LeftHand | `DEF_HAND.L` |
| RightShoulder | `DEF_SHOULDER.R` |
| RightUpperArm | `DEF_ARM.R` |
| RightLowerArm | `DEF_FOREARM.R` |
| RightHand | `DEF_HAND.R` |
| Left Thumb Proximal | `DEF_THUMB_01.L` |
| Left Thumb Intermediate | `DEF_THUMB_02.L` |
| Left Thumb Distal | `DEF_THUMB_03.L` |
| Left Index Proximal | `DEF_FINGER_A_01.L` |
| Left Index Intermediate | `DEF_FINGER_A_02.L` |
| Left Index Distal | `DEF_FINGER_A_03.L` |
| Left Middle Proximal | `DEF_FINGER_B_01.L` |
| Left Middle Intermediate | `DEF_FINGER_B_02.L` |
| Left Middle Distal | `DEF_FINGER_B_03.L` |
| Left Ring Proximal | `DEF_FINGER_C_01.L` |
| Left Ring Intermediate | `DEF_FINGER_C_02.L` |
| Left Ring Distal | `DEF_FINGER_C_03.L` |
| Left Little Proximal | `DEF_FINGER_D_01.L` |
| Left Little Intermediate | `DEF_FINGER_D_02.L` |
| Left Little Distal | `DEF_FINGER_D_03.L` |
| Right Thumb Proximal | `DEF_THUMB_01.R` |
| Right Thumb Intermediate | `DEF_THUMB_02.R` |
| Right Thumb Distal | `DEF_THUMB_03.R` |
| Right Index Proximal | `DEF_FINGER_A_01.R` |
| Right Index Intermediate | `DEF_FINGER_A_02.R` |
| Right Index Distal | `DEF_FINGER_A_03.R` |
| Right Middle Proximal | `DEF_FINGER_B_01.R` |
| Right Middle Intermediate | `DEF_FINGER_B_02.R` |
| Right Middle Distal | `DEF_FINGER_B_03.R` |
| Right Ring Proximal | `DEF_FINGER_C_01.R` |
| Right Ring Intermediate | `DEF_FINGER_C_02.R` |
| Right Ring Distal | `DEF_FINGER_C_03.R` |
| Right Little Proximal | `DEF_FINGER_D_01.R` |
| Right Little Intermediate | `DEF_FINGER_D_02.R` |
| Right Little Distal | `DEF_FINGER_D_03.R` |

Jaw, olhos e UpperChest ficam sem mapeamento.

## Clips (26)

Todos: Loop Time **on**, Mirror **off**, Root Transform Rotation / Position (Y) / Position (XZ) com **Bake Into Pose on** e **Based Upon = Original**, Height From Feet off, sem máscara. Takes `Armature\|Action` e `Armature\|Action.002` não viram clip.

| Clip | Take | Frames | Loop | Root (keep/bake) | Mirror | Eventos (tempo normalizado) |
|---|---|---|---|---|---|---|
| Idle | `Armature\|Idle` | 0–199 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Walk_Forward | `Armature\|Walk` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.05172414<br>AnimationFootstep @ 0.5517242 |
| Walk_Forward_Left | `Armature\|Walk_Left_45` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.05172414<br>AnimationFootstep @ 0.5603449 |
| Walk_Forward_Right | `Armature\|Walk_Right_45` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.05172414<br>AnimationFootstep @ 0.5517242 |
| Walk_Backward | `Armature\|Walk_Backward` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.06034483<br>AnimationFootstep @ 0.5258621 |
| Walk_Backward_Left | `Armature\|Walk_Backward_Left_45` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.05172414<br>AnimationFootstep @ 0.5344828 |
| Walk_Backward_Right | `Armature\|Walk_Backward_Right_45` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.06034483<br>AnimationFootstep @ 0.5258621 |
| Walk_Left | `Armature\|Walk_Left` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.025862107<br>AnimationFootstep @ 0.43965518 |
| Walk_Right | `Armature\|Walk_Right` | 0–29 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.025862107<br>AnimationFootstep @ 0.43965518 |
| Run_Forward | `Armature\|Run` | 12–35 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.010869566<br>AnimationFootstep @ 0.5326087 |
| Run_Forward_Left | `Armature\|Run_Left_45` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.010869566<br>AnimationFootstep @ 0.53260875 |
| Run_Forward_Right | `Armature\|Run_Right_45` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.010869566<br>AnimationFootstep @ 0.5434783 |
| Run_Backward | `Armature\|Run_Backward` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.0652174<br>AnimationFootstep @ 0.5543479 |
| Run_Backward_Left | `Armature\|Run_Backward_Left_45` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.0652174<br>AnimationFootstep @ 0.5543479 |
| Run_Backward_Right | `Armature\|Run_Backward_Right_45` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.0652174<br>AnimationFootstep @ 0.5543479 |
| Run_Left | `Armature\|Run_Left` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.4891305<br>AnimationFootstep @ 0.9891305 |
| Run_Right | `Armature\|Run_Right_1` | 0–23 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | AnimationFootstep @ 0.4891305<br>AnimationFootstep @ 0.9891305 |
| Crouch_Idle | `Armature\|Crouch` | 0–99 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Forward | `Armature\|Walk_Crouch` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Forward_Left | `Armature\|Walk_Crouch_Left_45` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Forward_Right | `Armature\|Walk_Crouch_Right_45` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Backward | `Armature\|Walk_Crouch_Backward` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Backward_Left | `Armature\|Walk_Crouch_Backward_Left_45` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Backward_Right | `Armature\|Walk_Crouch_Backward_Right_45` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Left | `Armature\|Walk_Crouch_Left` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |
| Crouch_Right | `Armature\|Walk_Crouch_Right` | 0–39 | sim | O=1 Y=1 XZ=1 bake O/Y/XZ=1/1/1 feet=0 | não | — |

Observação: no export novo (16 MB), o take `Armature\|Run_Right_1` passou a se chamar `Armature\|Run_Right`.

## internalID dos clips (o PlayerAnimator.controller aponta para eles)

| Clip | internalID |
|---|---|
| Idle | `-8945691123924629249` |
| Walk_Forward | `8349542763059015795` |
| Walk_Forward_Left | `8497993385330426665` |
| Walk_Forward_Right | `-2080418729114848475` |
| Walk_Backward | `-6224693340146862186` |
| Walk_Backward_Left | `4748191654737366913` |
| Walk_Backward_Right | `2571431415581269103` |
| Walk_Left | `-1088541377985981836` |
| Walk_Right | `-9202759852426119499` |
| Run_Forward | `-7551964380084998518` |
| Run_Forward_Left | `-5682975468756001599` |
| Run_Forward_Right | `-7485677388200325472` |
| Run_Backward | `-126748951450844125` |
| Run_Backward_Left | `4049655789016329298` |
| Run_Backward_Right | `-1997238130519137479` |
| Run_Left | `311081372004949835` |
| Run_Right | `8453097376229461452` |
| Crouch_Idle | `6589755540219670814` |
| Crouch_Forward | `4055677776718304845` |
| Crouch_Forward_Left | `-88354806484886029` |
| Crouch_Forward_Right | `-2010730274092584287` |
| Crouch_Backward | `1945094102557832168` |
| Crouch_Backward_Left | `2008340581715507231` |
| Crouch_Backward_Right | `-6957308219277603693` |
| Crouch_Left | `7790198521274459410` |
| Crouch_Right | `-1717959658605033456` |

## Fora do FBX (não dependem deste .meta)

- `Assets/Animations/Player/Interactions/GetItem.anim` e `HoldItem.anim`: clips Humanoid (curvas de músculo). Só tocam com rig Humanoid. `HoldItem` está com Mirror ligado (pendência antiga).
- `Upper Layer` do PlayerAnimator usa um AvatarMask (guid `57258476c2b4e4f4e9ec85f0499228a9`).
