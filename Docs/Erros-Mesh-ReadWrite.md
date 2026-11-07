# Erros de mesh sem Read/Write

Levantado em 06/11/2026, em partida real na cena `Game` com host e client. Cada peer gera 130 erros por partida, os mesmos nos dois lados.

## O que arrumar

São **17 arquivos FBX** com **24 meshes**. Em cada FBX: aba **Model**, marcar **Read/Write**, **Apply**.

### `Assets/Models/Box_Office/` (13 FBX)

| FBX | Meshes com erro | Erros por partida |
|---|---|---:|
| `SM_Stanchion.fbx` | `SM_Stanchion` | 39 |
| `SM_Stanchion_Solo.fbx` | `SM_Stanchion_Solo` | 13 |
| `SM_Cash_Register.fbx` | `SM_Cash_Register`, `SM_Cash_Register_Box` | 12 |
| `SM_Popcorn_Machine.fbx` | `SM_Popcorn_Machine_Low`, `Glass_Left`, `Glass_Right` | 12 |
| `SM_Wall_Cabinet.fbx` | `SM_Wall_Cabinet`, `SM_Door_Wall_Cabinet_1`, `_2`, `_3`, `_4` | 10 |
| `SM_Menu_Board_Small_1.fbx` | `SM_Menu_Board_Small_1` | 6 |
| `SM_Menu_Board_Small_2.fbx` | `SM_Menu_Board_Small_2` | 6 |
| `SM_Menu_Board_Big_2.fbx` | `SM_Menu_Board_Big_2` | 4 |
| `SM_Soda_Machine.fbx` | `SM_Soda_Machine_Low` | 4 |
| `SM_Menu_Board_Big_1.fbx` | `SM_Menu_Board_Big_1` | 3 |
| `SM_Tickets_Sign_New.fbx` | `SM_Tickets_Sign` | 2 |
| `SM_Signage_Support.fbx` | `SM_Signage_Support` | 1 |
| `SM_Snacks_Sign_New.fbx` | `SM_Snacks_Sign` | 1 |

### `Assets/Models/Audience_Objects/` (4 FBX)

| FBX | Mesh com erro | Erros por partida |
|---|---|---:|
| `SM_Vinyl_4.fbx` | `SM_Vinyl_4` | 5 |
| `SM_Vinyl_1.fbx` | `SM_Vinyl_1` | 4 |
| `SM_Vinyl_2.fbx` | `SM_Vinyl_2` | 4 |
| `SM_Vinyl_3.fbx` | `SM_Vinyl_3` | 4 |

## De onde vêm os erros

**NavMesh, 118 erros, nos 24 meshes.** Mensagem: `RuntimeNavMeshBuilder: Source mesh X does not allow read access`. Origem: `SpawnRooms.RebuildNavMeshRpc` ([SpawnRooms.cs](../Assets/Scripts/Rooms/SpawnRooms.cs)), que reconstrói o NavMesh quando as salas nascem. Segundo a própria mensagem da Unity, funciona no Editor, mas não no build. Sem a leitura do mesh, o esperado é que esses objetos fiquem de fora do NavMesh no build, e o monstro não desvie deles. Isso não foi testado em build.

**Contorno dos vinis, 12 erros, nos 4 `SM_Vinyl`.** Mensagem: `Not allowed to access vertices`, `normals` e `uv4 on mesh`. Origem: `Outline.LoadSmoothNormals` ([Outline.cs](../Assets/Scripts/Components/Outline.cs)), chamado no `Awake`. Acontece já no Editor. Pelo código, o contorno fica sem as normais suavizadas; o efeito visual não foi conferido.

## Onde os objetos aparecem

- `Box_Office/`: cena `Game` e prefab `Box_Office_Room`.
- `SM_Wall_Cabinet.fbx`: também em `BaseRoom_Decorated_5` e `Proto_BaseRoom_Decorated_5`.
- `SM_Vinyl_1` e `SM_Vinyl_4`: cena `Game` e prefabs `Dressing_Room` e `Dressing_Room 1`.
- `SM_Vinyl_2` e `SM_Vinyl_3`: só na cena `Game`.

## A lista está completa?

Sim, para as fontes abaixo. As salas são sorteadas, então além da partida foi feita uma varredura na cena `Game`, nos 17 `RoomDataSO` e nos 90 prefabs de rede. Ela previu exatamente os mesmos 24 meshes da partida.

Outros 29 meshes dessas fontes também estão sem Read/Write, mas **não precisam mudar**: estão em layers fora do NavMesh e sem `Outline`, então não geram erro. Passariam a gerar se forem movidos para uma layer do NavMesh ou ganharem `Outline`.

## Depois de arrumar

1. Fazer o reimport só no Editor principal. Marcar Read/Write mantém uma cópia do mesh na CPU, então o uso de memória sobe um pouco.
2. Desligar e religar o Player 2 do MPPM, se estiver aberto, para ele pegar o reimport.
3. Rodar uma partida na `Game` e conferir que o console não mostra mais `does not allow read access` nem `Not allowed to access`.
