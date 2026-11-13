# Tutorial

Cena separada que ensina o jogo do começo ao fim, falando com o jogador pelo **chat de verdade** do
jogo. Entra pelo botão **TUTORIAL** do menu principal (no armário ao lado de CRÉDITOS) ou dando Play
direto em `Assets/Tutorial/Scenes/Game0_Tutorial.unity`.

Nenhum script do jogo foi alterado. Tudo mora em `Assets/Tutorial/`.

## As 12 lições

| # | Lição | O jogador faz | O tutorial confere por |
|---|---|---|---|
| 1 | Movimento | anda até a marca, corre (Shift) o corredor, agacha (Ctrl) | zona no chão, `OnRunEvent`, `IsCrouching` |
| 2 | Passar por baixo | cano a 1,3 m e mesa a 1,08 m (o chapéu amassa); duto de 0,8 m mostra o limite | zonas depois de cada obstáculo |
| 3 | Lanterna | sala escura: liga (F) e acha a saída pela luz verde | estado da lanterna, zona da saída |
| 4 | Chat | esconde e mostra o chat (C); acha o botão escondido com dicas dos viewers | `HudAnimationPanel.IsVisible`, botão |
| 5 | Itens e bateria | pega a pilha, segura (1), usa e vê a barra encher; mira na caixa INDISPONÍVEL | inventário, `IsHoveredBlocked` |
| 6 | Portas | porta travada chacoalha; abre a outra, passa e fecha | `Door.OnDoorBlocked`, `Door.IsClosed` |
| 7 | Audiência | grava dois objetos até o olho ficar vermelho | `AudienceContributor.HasBeenSeen` |
| 8 | Missão e puzzle | recebe a missão, esconde/mostra o painel (V), lê título e dica, resolve as caixas | `PuzzleManagerBase.IsComplete` |
| 9 | Missão de gravação | missão principal (Laerte): grava o monstro no palco a partir da marca no chão | `PlayerMissionHolder.OnMainMissionCompleted` |
| 10 | Doação | doação "grave o monstro 5 s"; B mostra os pedidos | estado na `DonationManager.NetworkStates` |
| 11 | Luzes (extra) | apagão: liga a lanterna e usa o quadro de luz | `MonsterSabotage.LightsOut` |
| 12 | Fugir do monstro | o monstro é solto; corre até a SAÍDA fechando as portas | zona da saída |

No fim, um cartão com **Voltar ao menu** e **Jogar de novo**.

Se o jogador empaca, o chat ajuda sozinho: primeiro um viewer, depois a moderadora, cada vez mais
direto (`WaitFor` no diretor). No tutorial ninguém morre: depois de cada golpe o jogador é curado, e
no terceiro golpe da fuga ela recomeça do início do corredor.

## Peças do jogo usadas (sem alteração)

Player, portas, pilha, caixas de missão, objetos de audiência, `MissionTotemGroup` (puzzle das caixas),
`MainStage`, monstro, quadro de luz e os managers `MissionManager`, `AudienceManager`,
`DonationManager`, `DonationAudioManager`, `ChatEventHub`, `MissionRegisterManager` e `SfxManager`.

A missão principal nasce do próprio `MissionManager` quando a missão do puzzle termina (contrato
`Assets/Tutorial/Data/TutorialContract.asset`, sem missões pessoais e com `MissionLaerte` como
principal). A doação é `Assets/Tutorial/Data/TutorialDonation.asset`, que nunca sai sozinha.

O que o tutorial faz por fora, por reflexão (campos privados, sem tocar no código):

- `ChatManager.OnMessageSent`: linhas da moderadora entram direto no painel, na hora certa.
- `DonationManager.SpawnDonation`: a doação chega quando a lição pede.
- `MonsterSabotage._lightsOut`: o apagão da lição 11.
- `Flashlight` (máximo, gasto e carga da bateria, só na instância do tutorial): veja os achados.
- `HostGameManager.NetworkServer`: o host sobe sem passar pelo lobby (mesmo caminho do GYM).

## Scripts

| Arquivo | Papel |
|---|---|
| `Scripts/TutorialDirector.cs` | O roteiro: as 12 lições, falas, objetivos e dicas. **É aqui que se muda texto.** |
| `Scripts/TutorialChat.cs` | Escreve no chat (moderadora) e manda falas de plateia para a `ChatDirector`. |
| `Scripts/TutorialHud.cs` | Faixa de objetivo (embaixo, no centro) e cartão do fim. |
| `Scripts/TutorialNetworkBootstrap.cs` | Sobe o host e cria o jogador (vindo do menu ou Play direto). |
| `Scripts/TutorialMonsterRemote.cs` | Congela o monstro no palco e solta na fuga. |
| `Scripts/TutorialGate.cs`, `TutorialZone.cs`, `TutorialButton.cs` | Portões entre as salas, zonas de chegada e o botão escondido. |
| `Scripts/TutorialKeys.cs` | Nome das teclas lido do `PlayerActions` (acompanha rebind no asset). |
| `Scripts/TutorialLauncher.cs` | O botão do menu chama `Launch()`. |
| `Editor/TutorialSceneBuilder.cs` | Gera a cena inteira. |

## Gerar a cena de novo

`Tools/TCC/Tutorial/Gerar cena do tutorial`. A cena é reconstruída do zero: mudanças feitas à mão
nela se perdem. Mude o gerador (posições, salas, luzes) e gere de novo. O NavMesh é assado junto
(`Scenes/Game0_Tutorial/NavMesh-Tutorial.asset`) e a cena entra no Build Settings.

O nome começa com `Game0` de propósito: chat, barra de audiência, REC e música só ligam em cenas que
`Enums.SceneNames.IsGame` reconhece como partida (`Game` ou `Game` + dígito).

## Limites

- Um jogador só (host). É uma aula, não uma partida.
- Sem voz: os componentes de voz saem da instância do jogador. O TTS da doação só fala se o Vivox
  estiver logado (vindo do menu com internet).
- Testado no Editor: Play direto na cena e o caminho real Bootstrap → menu → TUTORIAL → voltar ao menu
  → criar partida LAN. Não testado em build.

## Achados no jogo (não corrigidos)

- **Bateria da lanterna infinita.** No `Player.prefab`, `batteryPercentMax` é `2147483647` e o gasto é
  2 por segundo: na partida a bateria nunca acaba e a barra nunca mexe. No tutorial ela vira 0–100.
- **Faixa de visão da gravação.** O sensor de visão do jogador só enxerga o que está numa faixa de
  1,3 m de altura em volta da mira. Na missão principal, mirar no corpo do monstro de uns 8 m deixa o
  gatilho do palco de fora se ele estiver baixo. No tutorial o gatilho fica na altura do monstro.
- **Botão Créditos** chama `MainMenu.OpenOptionPanel` além do `ButtonCameraTrigger` (talvez devesse
  ser `OpenCreditsPanel`).
