# Tutorial

Cena separada que ensina o jogo do começo ao fim, falando com o jogador pelo **chat de verdade** do
jogo. Entra pelo botão **TUTORIAL** do menu principal (no armário ao lado de CRÉDITOS) ou dando Play
direto em `Assets/Tutorial/Scenes/Game0_Tutorial.unity`.

Nenhum script do jogo foi alterado. Tudo mora em `Assets/Tutorial/`.

## As 12 lições

| # | Lição | O jogador faz | O tutorial confere por |
|---|---|---|---|
| 1 | Movimento | anda até a marca e corre (Shift) o corredor; a porta abre ao chegar no fim | zona no chão, `OnRunEvent` |
| 2 | Agachar | agacha (Ctrl), passa sob o duto de ar a 1,3 m e a mesa a 1,08 m (o chapéu amassa); grade de 0,8 m mostra o limite | `IsCrouching`, zonas depois de cada obstáculo |
| 3 | Lanterna | sala escura: liga (F) e acha a porta com a luz verde | estado da lanterna, zona da saída |
| 4 | Chat | esconde e mostra o chat (C); acha o botão escondido (o botão vermelho do puzzle de luz, atrás do armário) com dicas do chat | `HudAnimationPanel.IsVisible`, botão |
| 5 | Itens e bateria | pega a pilha, segura (1), usa e vê a barra encher; mira na caixa INDISPONÍVEL (o marcador dela só aparece com a mira em cima, sem o "PEGAR" fantasma ao chegar perto: `revealDistance` 0,01 só nesta cena) | inventário, `IsHoveredBlocked` |
| 6 | Portas | porta travada chacoalha; abre a outra, passa e fecha | `Door.OnDoorBlocked`, `Door.IsClosed` |
| 7 | Audiência | grava dois objetos até o olho ficar vermelho | `AudienceContributor.HasBeenSeen` |
| 8 | Missão e puzzle | recebe a missão "Exaustor", esconde/mostra o painel (V), lê título e dica, gira os canos da parede | `PuzzleManagerBase.IsComplete` |
| 9 | Missão de gravação | missão principal (Laerte): grava o monstro no palco a partir da marca no chão | `PlayerMissionHolder.OnMainMissionCompleted` |
| 10 | Doação | doação "grave o monstro 5 s"; B mostra os pedidos | estado na `DonationManager.NetworkStates` |
| 11 | Luzes (extra) | apagão: liga a lanterna e usa o quadro de luz | `MonsterSabotage.LightsOut` |
| 12 | Fugir do monstro | passa pela porta do fundo do palco, o monstro é solto; corre até a SAÍDA fechando as portas | zona da saída |

No fim, um cartão com **Voltar ao menu** e **Jogar de novo**.

## Como o tutorial fala e espera

- **Quem ensina:** S4lev, camisolinha, aaaquerodormir e tutujooj, com a mesma grafia da plateia do
  jogo (`ViewerPopulation`), para não aparecerem duas versões da mesma pessoa no chat. No começo os
  quatro se apresentam e pedem para o jogador prestar atenção no chat.
- **Quem fala é sorteado por assunto.** Cada assunto (andar, correr, agachar, lanterna, o botão
  escondido, a caixa INDISPONÍVEL...) é puxado por um dos quatro, sorteado, nunca quem puxou o
  assunto anterior. Quem começa termina: falas, dicas e comentário final do assunto saem com o mesmo
  nome (`NewTopic` no diretor). Na despedida os outros três também falam.
- **Uma frase por mensagem.** Nenhuma frase é cortada entre duas mensagens: cada fala é uma frase
  curta (até 30 letras, o que cabe com o nome mais comprido sem o chat encolher a fonte). Fala maior
  avisa no Console ("fala longa para o chat").
- **Uma sala por vez.** As falas de uma sala só começam quando o jogador entra nela (zona na
  entrada de cada sala); até lá a faixa diz "A porta abriu! Vá para a próxima sala". E a porta da
  próxima sala só abre depois que a turma terminou de falar e a última mensagem ficou o tempo de
  leitura na tela (`OpenNext` → `WaitSpeech`). Assim o jogador não vê fala de bateria na sala do
  chat, nem fala de uma sala que ainda não abriu.
- **Ação antecipada vale.** Cada lição confere a ação a todo quadro. O que o jogador fez antes de
  pedirem (agachar no saguão, ligar a lanterna antes da sala escura, esconder o chat logo ao entrar)
  passa na hora, e a fala que ensinaria a ação já feita é descartada.
- **Só quem ensina fala.** A conversa da plateia e as reações automáticas do jogo ficam mudas no
  tutorial (a fila da `ChatDirector` é esvaziada a cada quadro).
- **Ritmo:** a primeira mensagem sai 3 s depois de o jogador nascer; depois, no mínimo 4,5 s entre
  mensagens (0,1 s por letra nas maiores). Aviso urgente (golpe do monstro, porta de lição ainda
  fechada) fura a fila e dá pelo menos 2 s depois da mensagem anterior.
- **Se o jogador empaca,** quem puxou o assunto manda dicas, cada vez mais diretas, cada uma uma vez
  só (`WaitFor`; o tempo só conta depois que as falas pendentes saíram).
- **Faixa de objetivo** no topo da tela, no centro (entre o painel de missões e o REC); na lição de
  doação ela desce para baixo do cartão de doação. Texto comprido encolhe para caber.
- **Marcas e placas somem** quando a etapa delas termina (`TutorialFloorHint`): fita amarela, "ANDE
  ATÉ AQUI", "CORRA ATÉ AQUI", "PILHA", o X de gravação, a faixa verde da saída, e também as placas
  de parede de cada lição ("1 · MOVIMENTO", "2 · AGACHAR", DUTO/MESA/GRADE, "6 · PORTAS", "QUADRO DE
  LUZ"...). "PALCO PRINCIPAL" fica, é cenário.
- **No tutorial ninguém morre:** depois de cada golpe o jogador é curado, e no terceiro golpe da fuga
  ela recomeça do início do corredor.

## Portas entre as salas

Cada sala termina numa porta de verdade do jogo (`Door_Black`), trancada com `Door.CloseAndLock`, e a
luz de emergência do jogo (`SM_Exit_Light`) em cima, do lado de quem chega: **vermelha** enquanto a
lição não acabou, **verde** quando acaba, e a porta abre sozinha (`ClearLock` + `ForceOpenFrom`, o mesmo
caminho do monstro quando força uma porta). Quem tenta abrir antes da hora ouve a porta chacoalhar e o
chat explica. A luz saiu do `LightSabotage`, então o apagão não mexe nela. O comentário automático do
`ChatEventHub` sobre porta trancada ("o monstro está segurando") fica desligado nesta cena
(`doorCommentRange` = 0 no override da cena), porque essas portas não são o monstro.

## Cenário

Tudo o que se vê é peça do cenário do jogo, sem copiar a cena Game: paredes `SM_Sophisticated_Wall`
(papel vermelho nas salas, lambri nos corredores e saguões), pisos e tetos do corredor, do camarim e do
saguão, portas com batente (`..._Single_Door_2m_V1`), luminárias do corredor e os móveis de cada tema:
saguão com pipoqueira e cordões, depósito com duto de ar e cases, camarim com penteadeira e armários de
ferro, almoxarifado com prateleiras, exposição com refletores, oficina de canos com bancada e peças, e
o auditório com o palco do jogo (60%), cortinas, caixas de som e três fileiras de poltronas.

A colisão de parede e piso é feita por caixas invisíveis do gerador (as peças de parede do jogo não
têm colisor); o NavMesh é assado por elas (`PhysicsColliders`), com as portas de fora.

O puzzle é o dos canos (`PipesManager`, missão "Exaustor"), na mesma pose da sala de canos do jogo,
com os canos de entrada e saída copiados dela (`MissionRoomPipes`). O layout da grade é sorteado pelo
próprio jogo (12 ou 18 peças).

## Peças do jogo usadas (sem alteração)

Player, portas, pilha, caixa de missão, objetos de audiência, `PipesManager` (puzzle dos canos),
`MainStage`, monstro, quadro de luz e os managers `MissionManager`, `AudienceManager`,
`DonationManager`, `DonationAudioManager`, `ChatEventHub`, `MissionRegisterManager` e `SfxManager`.

A missão principal nasce do próprio `MissionManager` quando a missão do puzzle termina (contrato
`Assets/Tutorial/Data/TutorialContract.asset`, sem missões pessoais e com `MissionLaerte` como
principal). A doação é `Assets/Tutorial/Data/TutorialDonation.asset`, que nunca sai sozinha.

O que o tutorial faz por fora, por reflexão (campos privados, sem tocar no código):

- `ChatManager.OnMessageSent`: as falas dos narradores entram direto no painel, na hora certa.
- `ChatDirector` (`ambientEnabled`, `_queue`, `_gapTimer`): cala a plateia durante o tutorial.
- `DonationManager.SpawnDonation`: a doação chega quando a lição pede.
- `MonsterSabotage._lightsOut`: o apagão da lição 11.
- `Flashlight` (máximo, gasto e carga da bateria, só na instância do tutorial): veja os achados.
- `HostGameManager.NetworkServer`: o host sobe sem passar pelo lobby (mesmo caminho do GYM).

## Scripts

| Arquivo | Papel |
|---|---|
| `Scripts/TutorialDirector.cs` | O roteiro: as 12 lições, falas, objetivos e dicas. **É aqui que se muda texto.** |
| `Scripts/TutorialChat.cs` | Escreve no chat (os narradores) e manda falas de plateia para a `ChatDirector`. |
| `Scripts/TutorialHud.cs` | Faixa de objetivo (no topo, no centro) e cartão do fim. |
| `Scripts/TutorialFloorHint.cs` | Marcas de chão que somem quando a etapa termina. |
| `Scripts/TutorialNetworkBootstrap.cs` | Sobe o host e cria o jogador (vindo do menu ou Play direto). |
| `Scripts/TutorialMonsterRemote.cs` | Prende o monstro no palco (em postura de combate, IdleCombat) e solta na fuga. |
| `Scripts/TutorialDoorGate.cs` | Porta do jogo trancada entre as salas, com a luz de emergência (vermelha/verde). |
| `Scripts/TutorialZone.cs`, `TutorialButton.cs` | Zonas de chegada e o botão escondido. |
| `Scripts/TutorialKeys.cs` | Nome das teclas lido do `PlayerActions` (acompanha rebind no asset). |
| `Scripts/TutorialLauncher.cs` | O botão do menu chama `Launch()`. |
| `Editor/TutorialSceneBuilder.cs` | Gera a cena inteira. |

## Botão no menu

O botão TUTORIAL fica na porta do armário ao lado de CRÉDITOS. O papel vermelho atrás dele
(`CreditsPlane (1)`, com `HoverMaterialChange`) acende pelo raio do mouse; ele ficava 9 mm atrás do
colisor do armário e nunca acendia. O `BoxCollider` dele foi engrossado 4 cm para a frente (o visual
não mudou).

## Gerar a cena de novo

`Tools/TCC/Tutorial/Gerar cena do tutorial`. A cena é reconstruída do zero: mudanças feitas à mão
nela se perdem. Mude o gerador (posições, salas, luzes) e gere de novo. O NavMesh é assado junto
(`Scenes/Game0_Tutorial/NavMesh-Tutorial.asset`) e a cena entra no Build Settings.

O gerador salva a cena duas vezes: na segunda, com a cena já em arquivo e no Build Settings, cada
objeto de rede ganha o seu `GlobalObjectIdHash`. Sem isso as portas ficam com o ID do prefab, todas
iguais, e o host não sobe vindo do menu. Se aparecer "IDs de rede repetidos" no resultado, gere de novo.

O nome começa com `Game0` de propósito: chat, barra de audiência, REC e música só ligam em cenas que
`Enums.SceneNames.IsGame` reconhece como partida (`Game` ou `Game` + dígito).

## Limites

- Um jogador só (host). É uma aula, não uma partida.
- Sem voz: os componentes de voz saem da instância do jogador. O TTS da doação só fala se o Vivox
  estiver logado (vindo do menu com internet).
- Testado no Editor com um bot que joga pelo `InputReader`: Play direto na cena, do começo ao fim, com
  as duas grades de canos. O caminho Bootstrap → menu → TUTORIAL → voltar ao menu → partida LAN foi
  testado na versão anterior (o código de rede não mudou depois). Não testado em build.
- O palco (0,9 m, apoiado no piso) tem uma escadinha na frente (`SM_Stair`). O monstro é solto no chão, ao lado do palco, perto da porta da fuga.

## Achados no jogo (não corrigidos)

- **Bateria da lanterna infinita.** No `Player.prefab`, `batteryPercentMax` é `2147483647` e o gasto é
  2 por segundo: na partida a bateria nunca acaba e a barra nunca mexe. No tutorial ela vira 0–100.
- **Faixa de visão da gravação.** O sensor de visão do jogador só enxerga o que está numa faixa de
  1,3 m de altura em volta da mira. Na missão principal, mirar no corpo do monstro de uns 8 m deixa o
  gatilho do palco de fora se ele estiver baixo. No tutorial o gatilho fica na altura do monstro.
- **Botão Créditos** chama `MainMenu.OpenOptionPanel` além do `ButtonCameraTrigger` (talvez devesse
  ser `OpenCreditsPanel`).
- **Rótulo "Pegar" no quadro de luz e nos canos.** O `InteractionMarker` dos prefabs `EletricCircuit` e
  `PipeJoint` ficou com o texto padrão "Pegar". No tutorial é trocado só nas peças da cena ("Religar" no
  override da cena; "Girar" nos canos quando o puzzle nasce); na partida continua "Pegar".
- **Ícones das missões** (corrigido em 2026-11-15): `Mission Box`, `Mission Lamps`, `Mission Pipes` e
  `Mission Sound` apontavam para sprites que não existiam mais (quadrado branco no painel). Agora usam os
  ícones de `Sprites/MissionsIcons`. A missão principal (`Mission Laerte`) não tem ícone nenhum.
- **Malha do `SM_Air_Ventilation_Body`** tem o UV quebrado: a textura sai esticada em listras (na cena
  Game também, 8 instâncias). O tutorial usa a `SM_Air_Ventilation_Body_Big`, que está certa.
