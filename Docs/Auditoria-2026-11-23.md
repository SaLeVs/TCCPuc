# Auditoria do jogo — 2026-11-23

Varredura por leitura de código (Assets/Scripts), compilação e o log do Editor. A primeira versão deste relatório não alterou código nem rodou nada em Play; a seção abaixo registra a rodada seguinte, em que cada item foi reproduzido em Play antes de ser corrigido.

## Rodada 2 — reprodução e correção (mesmo dia)

Reproduzido no Editor com host + jogador virtual do MPPM (LAN e online pelo Lobby/Relay), e o C1 com a build rodando atrás de um proxy inválido (serviços da Unity inalcançáveis).

| Item | Reproduziu? | Correção | Reteste |
|---|---|---|---|
| C1 sem internet | Sim: build parada 50 s, sem menu | Menu abre sempre; tempo limite de 8 s/12 s nos serviços; Vivox fora do login; retry de verdade; `PlayerVoiceIdentity` sem serviço; aviso "use o modo LAN" no online (`OnlineServices`, `ApplicationController`, `ClientGameManager`, `AuthenticationController`, `MainMenu`) | Development Build, host + client com serviços bloqueados: menu em ~14 s, partida LAN até o Game (29 portas ok), 0 erros |
| C2 2ª partida online | Sim (com o host pronto: `StartGame` recusa com o estado que sobrava) | Win/Lose → menu sai da voz e do Lobby; queda do host também (`NetworkClientManager`); criar/entrar em lobby zera `_hasJoinedGame`; `LeaveLobby` não quebra quando os outros já saíram | 2ª partida iniciou |
| C3 monstro preso | Sim: parado em Chase com alvo nulo | `MonsterBrain.PruneDestroyedTargets` | voltou a andar |
| C4 missões de quem sai | Sim | Desconexão repassa missões (`MissionTransferManager` → `HandlePlayerLeft`) e solta itens e disquete (`PlayerDropper.OnNetworkDespawn`; o callback de desconexão chega depois do despawn) | missões no host, item e disquete no ponto de perdidos |
| C5 redistribuição em LAN | Sim, e pior: **as salas também eram geradas de novo** por cima | `MissionManager` e `SpawnRooms` só geram uma vez | total ficou 4, nada duplicado |
| A1 quadro de prontos online | Sim: "1 de 2" para sempre | Online, saída reduz o esperado em 1 (`PlayerTracker`) | contagem começou |
| A2 LAN aceita qualquer um | Em parte: quem entra no meio ganha corpo, mas fica sem missão (5º jogador não testado) | `NetworkServer` recusa com 4 jogadores ou com a partida em andamento, com mensagem | recusado: "Essa partida já começou..." |
| A3 travas de input | Sim: travado, pegou item e trocou de slot | `PlayerInteractor` e `PlayerInventory` respeitam a trava | travado não faz nada; destravado normal |
| M1 voz após Win/Lose | Sim | junto com C2 | menu sem canal ativo |
| M2 painel elétrico no client | Sim | `ElectricCircuit.CanInteract` usa o flag replicado | client: falso com luz, verdadeiro no blecaute |
| M3 ouvinte do Lobby | Sim (com ouvinte de teste que lança exceção) | início checado antes de avisar a UI; evento protegido | client entrou mesmo com o ouvinte quebrado |
| M4 erro por frame | Sim, ~100/s, **só com a cena aberta direto no Editor** | `VoiceLevelMeter` procura o VivoxManager sem logar, 1×/s | — |
| M5 porta no chat | O valor diverge, mas **nada no client lê isso** (o chat só olha a luz) | sem mudança | — |

Correção ao texto original: `VivoxManager.EnterLobbyVoice()` não é código morto (é chamado ao entrar no lobby).

Ainda em aberto: no online, alguém que **nunca** consegue conectar no Relay (não é saída, é falha de entrada) ainda deixa o quadro esperando; dois PCs reais na LAN não foram testados; o teste com 5 jogadores não foi feito (só um jogador virtual disponível).

Compilação: `dotnet build TCCPuc.sln` → 0 erros. Avisos nossos: só `PlayerMovement.blendMovementTime` sem uso e `EndScreenNameTag` usando `enableWordWrapping` obsoleto.

---

## Crítico — pode impedir a apresentação ou travar a partida

### C1. Sem internet/serviços da Unity, o menu nunca abre
`Network/ApplicationController.cs:32`, `Network/AuthenticationController.cs:42–56`

O menu só carrega se a autenticação passar, e `VivoxService.InitializeAsync()` está no mesmo `try` da autenticação. Se a rede da faculdade bloquear Unity Auth **ou só o Vivox**, o jogo fica na tela preta — inclusive para jogar em LAN, que não precisa de nada disso. Além disso, um erro põe o estado em `Error` e o `while` sai na primeira falha (o "retry" não tenta de novo).

Isso já está descrito em `Docs/Build-Lan-Faculdade.md` (P1, P2, P5) com as correções propostas, mas **as correções não foram aplicadas** no código atual.

**Como confirmar:** abrir a build com a internet desligada.

### C2. Segunda partida online na mesma sessão não começa (depois da tela Win/Lose)
`Ui/EndScreen/EndScreenController.cs:129`, `Network/Lobby.cs:64, 85, 380, 416`

O botão "Menu principal" da Win/Lose só faz `NetworkManager.Shutdown()` + `LoadScene`. Não chama `Lobby.LeaveLobby()` (único lugar que zera `_hasJoinedGame`), nem sai do canal de voz, nem descarta o `HostGameManager`. `CreateLobbyAsync`/`JoinLobbyByCodeAsync` também não zeram `_hasJoinedGame`. Resultado na próxima partida online:
- host: `StartGame()` retorna `false` logo na linha 380 — o botão INICIAR não faz nada;
- clients: o polling do Lobby para na linha 85 — nunca percebem que a partida começou.

O mesmo acontece com quem cai porque o host saiu (`NetworkClientManager` carrega o menu direto, sem `LeaveLobby`). O caminho pelo Pause → Menu está certo (chama `LeaveLobby` e sai da voz).

**Como confirmar:** jogar online até Win/Lose, voltar ao menu, criar outro lobby e tentar iniciar.

### C3. Jogador que sai da partida trava o monstro, se o monstro estiver vendo ele
`Components/VisionSensor.cs:121`, `Monster/MonsterStates/MonsterRoot.cs` (`GetTransitionState`), `Monster/MonsterChase.cs` (`ChaseUpdate`)

Quando o player é destruído por desconexão, o `VisionSensor` pula objetos nulos e **nunca dispara o "saiu da visão"**. O `Transform` destruído fica em `MonsterBrain._playersInVision`, então `Count > 0` para sempre: o monstro fica preso no estado Hunt, o `ChaseUpdate` não acha alvo, chama `ClearTarget()` (para o agente) e ele fica parado até alguém entrar no cone de visão. A morte não tem esse problema (os colliders desligam antes).

**Como confirmar:** host + client, deixar o monstro perseguir o client e fechar o client.

### C4. Missões de quem sai da partida não são repassadas — a vitória fica impossível
`Missions/MissionTransferManager.cs:62`, `Missions/MissionManager.cs` (`HandlePlayerDeath`), `Player/PlayerDroppper.cs:26`

Missões só são transferidas na **morte**. Na **desconexão**, o `MissionTransferManager` apenas remove o registro. As missões do jogador continuam contando em `_totalPersonalMissions`, ninguém é dono das peças (só o dono pode pegar itens/mexer no puzzle) e a missão principal nunca aparece. Os itens do inventário também somem (o `PlayerDropper` só solta itens na morte).

**Como confirmar:** 2 jogadores, um sai antes de terminar as missões dele.

### C5. Em LAN, alguém sair no meio da partida redistribui todas as missões
`Components/PlayerTracker.cs:108–122`, `Missions/MissionManager.cs:54, 214, 268`

No modo LAN, a desconexão faz `AreAllPlayersConnected` ir de verdadeiro → falso (contagem cai) → verdadeiro (o "esperado" é ajustado para a nova contagem). Essa volta dispara `OnAllPlayersConnected` de novo, e o `MissionManager` roda `DistributeMissionsForPlayers()` **outra vez no meio da partida**: sorteia de novo, manda missões duplicadas para a HUD, soma `_totalPersonalMissions` em dobro (a missão principal passa a exigir o dobro de missões concluídas) e não reatribui os donos (porque `_ownersAssigned` já é verdadeiro). Combinado com C4, a partida não tem como ser vencida.

**Como confirmar:** LAN com 3 jogadores, um sai depois do início.

---

## Alto

### A1. Online: alguém sair do lobby de rede antes de começar trava o quadro de prontos
`Network/Lobby.cs:394`, `Systems/PlayersReady.cs` (`AreAllReady`), `Components/PlayerTracker.cs`

No online, o número esperado de jogadores é fixado em `_joinedLobby.Players.Count` quando o host clica INICIAR, e não diminui quando alguém sai (só o modo LAN ajusta). Se alguém sai ou não consegue conectar no Relay, o quadro nunca chega em "todos prontos" e, se chegar ao Game, as missões nunca são distribuídas (`OnAllPlayersConnected` não dispara).

### A2. LAN aceita qualquer conexão, inclusive no meio da partida e acima de 4 jogadores
`Network/NetworkServer.cs:30`, `Network/SpawnManager.cs:34`

`ConnectionApproval` aprova sempre. O limite de 4 só existe no Relay/Lobby online. Em LAN, um 5º jogador ou alguém entrando com a partida rolando é aceito; o `SpawnManager` só cria player em `LoadComplete` (late join chega por sincronização, não por load), então essa pessoa fica sem corpo, sem câmera de jogo e sem missão. Sugestão: recusar conexões quando a cena atual não é o Lobby ou quando já há 4.

### A3. Travas de input não bloqueiam a tecla de interagir nem os slots
`Player/PlayerInteractor.cs:154` (`Interact`), `Player/PlayerInventory.cs:65` (`InputReader_OnSlotEvent`)

Os dois guardam `_isLocked`, mas não usam no `Interact()`/`Update()`/troca de slot. Durante minigame, quadro de prontos, knockdown e fim de jogo:
- a mesma tecla que confirma o minigame também dispara `Interact()` no que estiver na mira;
- caído (knockdown), dá para abrir porta/pegar item ao alcance;
- dá para equipar item com 1–4 enquanto travado (o item aparece na mão do corpo caído).

**Como confirmar:** apertar E/1–4 durante o skill check e durante o knockdown.

---

## Médio

### M1. Canal de voz continua ativo depois de voltar ao menu pela Win/Lose ou por queda do host
Mesmo caminho de C2: só o Pause chama `VivoxManager.LeaveVoiceChannel()`. Os jogadores continuam no canal posicional da partida anterior até entrarem em outra.

### M2. Painel elétrico aparece como "interagível" para clients mesmo com a luz normal
`Systems/ElectricCircuit.cs:25` — no client `CanInteract` retorna sempre `true`. O host vê certo. O client vê o contorno/prompt e o E não faz nada.

### M3. Exceção em quem escuta o Lobby impede o client de entrar na partida
`Network/Lobby.cs:98` — `OnLobbyUpdated` é chamado dentro do `try` do polling, antes de `CheckIfGameStarted()`. Se qualquer UI inscrita lançar exceção, o client nunca checa o início da partida. O log do Editor de hoje tem 26× "Lobby poll failed: Object reference not set..." (vindo do `LobbyUi.UpdateStartButtonText` — pode ser anterior ao commit 5a7d8f87, que arrumou o `startButtonText`; vale confirmar). Sugestão: chamar `CheckIfGameStarted()` antes do evento, ou proteger o evento.

### M4. `VoiceLevelMeter` gera erro todo frame quando não há VivoxManager
`Ui/VoiceLevelMeter.cs:45` — chama `VivoxManager.instance` no `Update`. Sem o manager (cena aberta direto no Editor, ou serviços fora do ar), cada frame faz `FindFirstObjectByType` + `Debug.LogError("VivoxManager not found")`. Foram 447 erros no log de hoje.

### M5. Sabotagem de porta é invisível para o chat dos clients
`Monster/MonsterSabotages/DoorSabotage.cs` — `IsSabotaged` lê `Door.IsLocked`, que usa um tempo que só existe no servidor. O `ChatEventHub` consulta `HasSabotagedOfType` em todas as máquinas; no client a porta nunca aparece sabotada, então o chat do client não reage à porta trancada pelo monstro (o do host reage).

---

## Baixo / limpeza

- `PlayerInventory.OnNetworkDespawn` não remove `OnPlayerLocked` (inofensivo hoje, porque vive e morre junto com o player).
- `MonsterBrain.OnNetworkDespawn` desinscreve eventos mesmo quando o spawn saiu cedo (por falta de Awareness) — inofensivo.
- `EndScreenController.ReturnToMainMenu` com o host clicando primeiro derruba os clients da tela de resultado antes deles lerem.
- `VivoxManager.EnterLobbyVoice()` não é chamado por ninguém (código morto) — no Lobby a voz entra pelo `EnterGameVoice` do `VivoxPlayer`.
- Voz só existe online: em LAN `JoinedLobby` é nulo e `EnterGameVoice` não faz nada. Se isso for intencional, vale avisar na apresentação.
- Avisos de compilação: `PlayerMovement.blendMovementTime` sem uso; `TMP_Text.enableWordWrapping` obsoleto em `EndScreenNameTag.cs:35`.

---

## O que foi conferido e está ok

- Fim de partida (`MatchEndDirector`): reavalia na morte, na fuga e na desconexão; vitória/derrota e cenas Win/Lose estão no Build Settings.
- Dano do monstro só no servidor (`MonsterHitbox`), cooldown no componente, slow pós-acerto, taunt/roar.
- Itens soltos na morte voltam a ser do puzzle (`PlayerDropper` + `MissionItemRegistry`); missões passam para os vivos na morte.
- HUD (inclusive o chat) dos outros jogadores é desligado na sua máquina (`PlayerSpectator`), então não há chat duplicado.
- Espectador: `ConnectedClients` funciona no client nesta versão do Netcode.
- Eventos estáticos: todo `+=` tem `-=` correspondente.

## O que NÃO foi auditado a fundo

Lido só por cima ou não lido: `Door.cs` (1654 linhas; o bug de sumir no client já foi validado), tutorial (`TutorialDirector`/`TutorialSceneBuilder`), doações/TTS, props chutáveis, espelhos, UI de opções e menus em World Space, cenas e prefabs (referências vazias no Inspector só aparecem em Play). Nada foi testado em Play nesta varredura.
