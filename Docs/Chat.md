# Chat de viewers — manual de controle

Auditoria completa do sistema de chat: como ele funciona, onde fica cada botão, e o passo a passo
para mudar qualquer coisa.

---

## 1. Como funciona

```
                      ┌─ VisionSensor ────────┐  (olhou pra algo)
                      │                       │
                      ├─ Health ──────────────┤  (dano / morte, de qualquer player)
                      │                       │
                      │                       │  ┌ viewers, pico, queda
   ChatStimulusBus ◄──┼─ ChatEventHub ────────┤──┤ doação, missão, dica de missão
   (barramento)       │  (assembly Game)      │  ├ porta trancada
                      │                       │  └ luz apagou, dica do gerador
                      ├─ ChatTrigger ─────────┤  (qualquer coisa, no inspector)
                      └─ ChatIdleWatcher ─────┘  (qualquer "travou", no inspector)
                                │
                                ▼
                         ChatManager          traduz estímulo → pool de falas
                                │
                                ▼
                         ChatDirector         fila com prioridade + ritmo + humor
                                │
                                ▼
                            ChatUi            formata cor, ícone e escreve na tela
```

**Três coisas que valem entender antes de mexer:**

1. **Tudo roda só no client dono do player.** O `VisionSensor` manda RPC só pro owner, e nada aqui
   escreve estado que outro jogador veja. Cada jogador tem o próprio chat.

2. **O barramento existe por causa de um ciclo de assembly.** `Missions` já referencia `Player`,
   então o chat (que mora em `Player`) nunca poderia referenciar `Missions` de volta — o Unity
   rejeita. `ChatStimulusBus` fica em `Components`, que os dois lados enxergam. Efeito colateral
   bom: qualquer sistema fala com o chat em **uma linha estática, sem referência nenhuma**.

3. **Evento é texto, não enum.** `ChatStimulusBus.Raise("tutorial.bem_vindo")` funciona sem
   recompilar nada e sem tocar em nenhum assembly. É o que torna o tutorial possível depois.

---

## 2. Onde fica cada botão

### 2.1 `ChatManager` — componente em `ChatBar.prefab` (aninhado em `Player.prefab`)

| Campo | O que faz |
|---|---|
| `visionSensor` | Sensor de visão do player. Já vem cabeado por override no `Player.prefab`. |
| `chatUi` | Objeto da UI que liga/desliga com a cena. |
| `director` | O componente `ChatDirector`, no mesmo objeto. Ver §2.2. |
| `messageDatabase` | Falas de **olhar coisas** (por `RecordableTarget`). |
| `topicDatabase` | Falas de **acontecimentos** (por ID de texto). |
| `ambientDatabase` | Conversa de fundo por humor. |
| `viewerPopulation` | Quem está assistindo. |
| `minMessages` / `maxMessages` | Quantas falas cada avistamento gera (1 a 3). |
| `normalIntensity` | Quanto o chat se agita ao olhar algo comum (0.25). |
| `monsterIntensity` | Idem, pro monstro (0.8) — é isso que deixa a sala tensa. |
| `hurtThreshold` | Fração da vida perdida de uma vez pra virar assunto (0.08). |
| `noveltyWindow` | Quanto tempo até um alvo contar como "novo" de novo (120s). |

### 2.2 `ChatDirector` — componente separado, no mesmo objeto do `ChatManager`

**O controle mais importante do sistema inteiro.** Era um bloco aninhado dentro do `ChatManager`;
virou componente próprio porque nove valores de tuning atrás de um foldout dentro de outro
componente é onde ninguém acha. O `ChatManager` tem `[RequireComponent]` dele, então o Unity
garante que os dois andam juntos.

| Campo | O que faz |
|---|---|
| **`linesPerMinuteByViewers`** | **Curva: X = nº de viewers, Y = falas por minuto.** É o dial mestre de volume. |
| `peakIntensityMultiplier` | Quantas vezes mais rápido no auge (6x). Multiplica o que a curva deu. |
| `minViewersToTalk` | Viewers mínimos pro chat abrir a boca (1). Abaixo disso só passa dica. |
| `minGapBetweenMessage` / `maxGapBetweenMessage` | Piso e teto do intervalo entre falas (0.35s / 20s). |
| `intensityDecayPerSecond` | Quão rápido a agitação baixa (0.12 ≈ 8s pra voltar ao normal). |
| `ambientEnabled` | Liga a conversa de fundo. Desligar deixa o chat mudo quando nada acontece. |
| `maxQueuedMessages` | Falas esperando na fila (14). Passou disso, abre espaço descartando a **mais antiga da faixa de menor prioridade**. |
| `maxLineAge` | Segundos que uma fala pode esperar antes de ser descartada (12s). |

**Curva padrão** (0 viewers → silêncio total):

| Viewers | Falas/min em repouso | No auge (×6) |
|---|---|---|
| 0 | 0 | 0 |
| 10 | 1,5 | 9 |
| 50 | 4 | 24 |
| 150 | 8 | 48 |
| 400 | 16 | 96 |
| 800 | 25 | 150 |
| 1200 | 34 | 204 |

> O jogo começa com `startingAudience: 0` e o contrato vai até `maxAudience: 1200`. A curva cobre
> exatamente essa faixa. É o mesmo número que a HUD imprime na barra de audiência.

#### Como uma fala some da fila

São **dois caminhos diferentes**, e eles usam critérios diferentes de propósito:

| Caminho | Quando | Critério |
|---|---|---|
| **Idade** (`DropStaleLines`) | Toda fala, `maxLineAge` segundos depois de entrar | **Só ordem de chegada.** Prioridade não é lida. Quem entrou antes morre antes, sempre — uma dica de prioridade 60 não vive um segundo a mais que conversa fiada. |
| **Lotação** (`TryMakeRoom`) | A fila chega em `maxQueuedMessages` | **Prioridade escolhe a faixa, idade escolhe quem.** A vítima é a mais antiga da faixa de menor prioridade. Se a fala nova for mais fraca que toda a fila, ela é que não entra. |

A divisão é intencional: prioridade serve pra **uma enxurrada de conversa fiada não expulsar uma
dica**, não pra dar sobrevida a quem já está velho. Por isso, dentro da mesma faixa, quem sai é
sempre a mais antiga — fila cheia continua reagindo ao *agora* em vez de guardar coisa velha e
recusar coisa fresca.

### 2.3 `ChatUi` — mesmo prefab

| Campo | O que faz |
|---|---|
| `activeMessagesCount` | Linhas visíveis na tela (9). |
| `messageFormat` | Template. Placeholders: `{icon}`, `{color}`, `{viewer}`, `{message}`. |
| `iconAppearChance` | Chance de um viewer ter badge (0.35). |
| `iconPerViewer` | Ligado: cada viewer tem sempre o mesmo ícone. |
| `iconPool` | Índices de sprite permitidos. Vazio = todos. |
| `nameColors` | Paleta de cores dos nomes. |

### 2.4 `ChatMessageDatabase.asset` — falas de avistamento

Por `RecordableTarget`. Cada entrada:

- `messages[]` — a lista de falas

Cada fala (`ChatMessage`):

| Campo | O que faz |
|---|---|
| `message` | O texto. Pode usar `{subject}`. |
| `weight` | Chance relativa dentro do pool. **0 desabilita.** |
| `allowedArchetypes` | Quais personalidades podem dizer isso (flags). |

### 2.5 `ChatTopicDatabase.asset` — falas de acontecimento

Por ID de texto. Além do `data` (igual acima):

| Campo | O que faz |
|---|---|
| `id` | O identificador. Ex.: `hint.door_locked`, `tutorial.bem_vindo`. |
| `minMessages` / `maxMessages` | Volume da reação. |
| `intensity` | **Teto** de agitação do tópico. O emissor escala isso. |
| `mood` | Humor que empurra na sala. |
| `cooldown` | Segundos até o tópico poder repetir. Rede de segurança pra quem dispara repetido (ex.: `player.hurt` com tick de dano). **Em dica, o freio de verdade é o `repeatSeconds` do nudge** — ver §2.7. |
| `priority` | Quem fura fila. Ver tabela abaixo. |
| `ignoreViewerFloor` | Fala mesmo com ninguém assistindo. **Ligado nas dicas.** |

**Escala de prioridade em uso:**

| Faixa | Quem |
|---|---|
| 50–60 | Dicas (`hint.*`) — precisam chegar |
| 15–20 | Reações grandes (morte, doação, missão) |
| 5–10 | Reações pequenas (dano, audiência, luz voltou) |
| 0 | Conversa de fundo |

### 2.6 `ChatAmbientDatabase.asset` — conversa de fundo

Uma entrada por humor. `secondsBetweenLines` é o intervalo base — escalado pela curva de viewers,
então chat pequeno também conversa menos de fundo.

| Humor | Quando | Intervalo padrão |
|---|---|---|
| `Idle` | Padrão. Nada acontecendo | 9s |
| `Bored` | Audiência caindo (`IsDecaying`) | 7s — chat entediado reclama **mais** |
| `Panic` | Monstro em cena ou perseguindo | 4s |

> Eram cinco. `Hype` e `Tense` saíram: ambiente só dispara com a fila vazia, e nesses dois humores
> a fila tinha acabado de encher de reação — eram pools que quase ninguém veria.

### 2.7 `ChatNudge` — a cutucada

O relógio que faz o chat ajudar quando nada acontece há tempo demais. Usado em três lugares:
exploração (no `ChatManager`), missão e luz (no `ChatEventHub`). **Três campos, só:**

| Campo | O que faz |
|---|---|
| `topicId` | Tópico que dispara. |
| `idleSeconds` | Segundos de nada até a primeira cutucada. |
| `repeatSeconds` | Intervalo das repetições. **0 = cutuca uma vez só.** |

**Presets atuais** — todos no `ChatEventHub.prefab`, editáveis no inspector:

| Dica | idle | repeat |
|---|---|---|
| Exploração | 75s | 50s |
| Missão | 90s | 60s |
| Luz apagada | 12s | 25s |

> **As três ficam no mesmo lugar:** o `ChatEventHub` na cena, sob `Hints`. A de exploração morava
> no `ChatManager` porque é ele que enxerga os avistamentos — hoje ele só avisa
> (`OnExploredSomethingNew`) e quem decide cutucar é o hub, junto das outras duas.

#### Qual freio mexer

Duas coisas controlam o intervalo de uma dica, e é fácil mexer na errada:

| Controle | Onde | Quando usar |
|---|---|---|
| **`repeatSeconds`** do nudge | prefab | **É este.** É o que define de quanto em quanto tempo a dica volta. |
| `cooldown` do tópico | `ChatTopicDatabase` | Rede de segurança pra tópicos disparados de vários lugares (ex.: `player.hurt`, que leva tick de dano). Se for menor que o `repeatSeconds`, **não faz absolutamente nada**. |

#### Por que não tem intensidade nem escalada

Duas rodadas de corte passaram por aqui. Primeiro saiu a escalada (`escalate`, `startIntensity`,
`maxIntensity`, `escalationSteps`), depois o próprio `intensity`. O motivo é o mesmo nos dois casos:
**intensidade só muda a velocidade do chat depois da fala, nunca o que é dito.**

Hoje a cutucada dispara sempre no nominal (0.5), o que cai exatamente no `intensity` do tópico. Um
número só no sistema inteiro, no lugar onde faz sentido.

> Se você quiser escalada de verdade um dia, o caminho é **um segundo tópico com texto mais direto**
> (`hint.lights_out` → `hint.lights_out_urgente`), disparado por um segundo nudge com `idleSeconds`
> maior. Aí muda o que está escrito, que é o que dá pra notar. E é dado, não campo novo.

### 2.8 `ViewerPopulation.asset` — quem assiste

| Campo | O que faz |
|---|---|
| `viewers[].name` | Nome que aparece. |
| `viewers[].archetype` | Personalidade (uma só por viewer). |
| `viewers[].chattiness` | Quanto fala. **É o que separa regular de figurante.** |

> **Fonte única.** As doações também tiram o nome do doador daqui, via `TryPickName`
> (`DonationDefinition.fakeDonorNames`). É o mesmo `chattiness` que decide, então os regulares
> que mais falam também são os que mais doam — o que é o comportamento realista pra uma live, e
> garante que nenhum doador seja um nome que o jogador nunca viu no chat.

**Arquétipos:** `Hype` (1), `Scared` (2), `Troll` (4), `Backseat` (8), `Lurker` (16).
`Everyone` = 31.

Distribuição atual: 6 regulares (6–8), 10 médios (2–3), 14 figurantes (0,6–1).

### 2.9 `RecordableIdentifier` — em cada objeto do mundo

| Campo | O que faz |
|---|---|
| `targetType` | Qual pool de avistamento usa. |
| `minimumViewTime` | Segundos olhando até o chat comentar. |
| `chatCooldown` | Segundos até poder comentar de novo sobre esse objeto. |
| `canBeReviewedForChat` | Desliga o objeto pro chat. |

> ⚠️ O valor "desligado" do enum é `None` (8), mas o default do C# é **0 = `Room`**. Objeto novo
> sem configurar vira alvo Room silenciosamente.

### 2.10 `ChatEventHub` — a ponte entre o jogo e o chat

`Assets/Scripts/ChatEventHub.cs`, assembly **`Game`**, ao lado do `SfxManager`.

#### O que é

O **único** lugar onde o jogo avisa o chat que algo aconteceu. Escuta os quatro sistemas e traduz
cada evento num tópico no `ChatStimulusBus`. Não sabe o que o chat vai dizer — só anuncia o ID.
Quem decide as falas é o `ChatTopicDatabase`.

```
DonationManager (NetworkList muda)
        ↓  ChatEventHub escuta
ChatStimulusBus.Raise("donation.received", 0.5f, "caldoDiCana")
        ↓  ChatManager escuta o barramento
procura "donation.received" no ChatTopicDatabase → sorteia falas → fila
```

#### Por que fica no assembly `Game`

Por um **ciclo de assembly**. A cadeia é:

```
Missions ──referencia──► Player ──onde mora──► ChatManager
```

Se o `ChatManager` referenciasse `Missions` pra escutar doações, fecharia `Player → Missions →
Player`, e o Unity **rejeita** isso — nem compila. O mesmo vale pra Audience, Objects e Monster.

`Game` é o único lugar que resolve, porque está no **topo** do grafo: referencia quase todo mundo e
**ninguém referencia ele**, então adicionar referências lá nunca pode criar ciclo. É exatamente
onde o `SfxManager` já vive, escutando 13 eventos estáticos de 6 assemblies — o hub segue o mesmo
padrão.

E o barramento continua em `Components` (o fundo do grafo, que todos enxergam), o que mantém a seta
apontando numa direção só. Efeito colateral bom: qualquer sistema fala com o chat em **uma linha
estática, sem referência nenhuma**. É a mesma porta que o tutorial vai usar.

> ⚠️ Não tente centralizar isso em `Components`. Ele é o fundo do grafo — 11 assemblies dependem
> dele e ele só referencia `Interfaces`, `Enums` e `Unity.Netcode`. Pra enxergar `DonationManager`
> precisaria de `Components → Missions`, e `Missions → Components` já existe. Ciclo garantido.

#### Como é criado

É um **prefab colocado na cena**: `Assets/Prefabs/Managers/ChatEventHub.prefab`, que deve estar na
`Game.unity` junto com os outros managers.

Não se cria sozinho de propósito. Um objeto que se instancia por `RuntimeInitializeOnLoadMethod` e
vive em `DontDestroyOnLoad` não aparece na cena, não dá pra inspecionar, e os dois `ChatNudge` dele
ficam fora do inspector — presos ao preset do construtor. Como objeto de cena, ele é visível na
hierarquia, dá pra pôr breakpoint, e **os tempos das dicas viram campos editáveis**.

> ⚠️ Se o hub não estiver na cena, nenhum evento do jogo vira chat: nem doação, nem missão, nem
> porta, nem luz. Os avistamentos continuam funcionando, porque esses vêm do `ChatManager`, que
> está no prefab do player. **É o primeiro lugar pra olhar** quando só os avistamentos funcionam.

#### Ciclo de vida

Como é objeto de cena, ele existe exatamente enquanto a partida existe — nenhum relógio dele pode
correr no menu principal. Mas os managers nascem por netcode um instante *depois* da cena, então
cada fonte ainda espera achar o que escuta:

| Estado | O que acontece |
|---|---|
| **Cena carregou, managers ainda não** | Procura a cada 1s (2s o de sabotagem). Reporta 0 viewers, então o chat fica mudo. Os relógios das dicas ficam congelados. |
| **Managers apareceram** | Liga nos eventos e reseta os relógios das dicas. |
| **Saindo da cena** | Desassina tudo e reporta 0 viewers, pra não deixar o barramento achando que ainda tem plateia. |

#### O que ele escuta

O arquivo é dividido em quatro blocos, nessa ordem, cada um com o comentário explicando a decisão:

| Bloco | Escuta | Levanta |
|---|---|---|
| **Audience** | `AudienceManager` | Reporta a contagem de viewers a cada 0,25s (é o que alimenta a curva de volume) + `audience.surge` / `audience.drop` |
| **Donations and missions** | `NetworkStates` (lista replicada) e eventos estáticos do `PlayerMissionHolder` | `donation.received`, `donation.expired`, `mission.completed`, `hint.mission_idle` |
| **Doors** | `Door.OnDoorBlockedSound` (estático) | `hint.door_locked` |
| **Lights** | `MonsterSabotage.HasSabotagedOfType(Light)` por polling | `lights.out`, `hint.lights_out`, `lights.restored` |
| **Exploration** | `ChatManager.OnExploredSomethingNew` do player local | `hint.exploration_idle` |

> **Luz apagando são dois tópicos, não um.** `lights.out` é a reação imediata — o chat surtando no
> instante em que o mundo escurece. `hint.lights_out` é a dica do gerador, e só aparece 12 segundos
> depois, repetindo a cada 25s. Separados porque servem a coisas diferentes: uma é
> susto, a outra é ajuda, e ajuda entregue rápido demais tira o susto do jogador.

> **A porta trancada só existe por causa do monstro.** Rastreei tudo que pode deixar
> `Door.IsLocked` verdadeiro: `MonsterDoorForcer.ForceOpenFrom` (o monstro arrombou) e
> `DoorSabotage.CloseAndLock` (o monstro sabotou). O jogo não tem porta com chave nem tranca de
> puzzle. Então `hint.door_locked` significa sempre "o monstro está segurando essa porta" — vale
> escrever as falas com esse tom, e não como "procura a chave".

#### Como configurar

Está dividido em dois lugares, de propósito:

| O quê | Onde | Editável no inspector? |
|---|---|---|
| **O que o chat diz, volume, humor, prioridade, cooldown** | `ChatTopicDatabase.asset` | ✅ sim — é aqui que você mexe 95% das vezes |
| **Quando o hub decide levantar o evento** | Constantes no topo do `ChatEventHub.cs` | ❌ não, é um GameObject criado em runtime |

As constantes estão todas agrupadas no início do arquivo:

| Constante | Padrão | Significado |
|---|---|---|
| `AudienceReportInterval` | 0,25s | Frequência com que a contagem de viewers é reportada |
| `NoticeableAudienceChange` | 12 | Viewers ganhos/perdidos de uma vez pro chat comentar |
| `BigAudienceChange` | 60 | Variação que conta como reação máxima |
| `BigDonation` | 100 | Valor de doação que conta como reação máxima |
| `DoorCommentRange` | 14m | Distância máxima da porta pro chat comentar |
| `SabotagePollInterval` | 0,5s | Frequência do teste da luz |
| `BindRetryInterval` | 1s | Frequência da procura pelos managers |

Mais os dois presets de `ChatNudge` (ver §2.7), que são `[SerializeField]` — se quiser editá-los no
inspector, troque o auto-bootstrap por um componente colocado na cena.

#### Como adicionar um evento novo

1. Abra `ChatEventHub.cs`, escolha o bloco (ou crie um novo com o mesmo separador de comentário).
2. Assine o evento em `OnEnable`, desassine em `OnDisable`. Se for um manager que só existe durante
   a partida, siga o padrão `TryBind*` + `TickBinding`.
3. Chame `ChatStimulusBus.Raise("seu.topico", intensidade, assunto)`.
4. Adicione a linha `seu.topico` no `ChatTopicDatabase` e escreva as falas.
5. Se o sistema estiver num assembly que `Game` ainda não referencia, adicione no `Game.asmdef` —
   é sempre seguro, ninguém referencia `Game`.

**Antes de mexer no hub, cheque se você precisa.** Se o evento é client-side e você consegue chamar
de um MonoBehaviour na cena, `ChatTrigger` ou `ChatIdleWatcher` resolvem sem tocar em código.

> ⚠️ **Armadilha de netcode:** confira se o evento que você vai escutar não está atrás de
> `if (!IsServer) return`. Foi o que aconteceu com `DonationManager.OnDonationCompleted` e
> `MissionCompleter.OnMissionCompleted` — assinar eles teria dado chat no host e silêncio em todos
> os outros clients. Quando o evento for server-only, procure o estado replicado equivalente
> (`NetworkVariable`, `NetworkList`) ou um RPC `SendTo.ClientsAndHost`.

---

## 3. Passo a passo

### 3.1 "O chat fala demais / de menos"

**Não mexa em `minGapBetweenMessage`/`maxGapBetweenMessage`.** Vá na curva.

1. `Player.prefab` → `ChatBar` → componente `ChatDirector` → `Lines Per Minute By Viewers`
2. Arraste os pontos. X = viewers, Y = falas/min.
3. Para cortar tudo pela metade: baixe todos os Y. Para calar chat pequeno: puxe os primeiros
   pontos pra perto do zero.

Se o problema é só **nos picos**, mexa em `peakIntensityMultiplier` em vez da curva.

### 3.2 "Quero adicionar falas a algo que já existe"

1. Abra o asset certo: avistamento → `ChatMessageDatabase`; acontecimento → `ChatTopicDatabase`;
   fundo → `ChatAmbientDatabase`.
2. Adicione uma linha em `messages`.
3. Escreva o texto, escolha `weight` (1 = comum, 5 = frequente).
4. Marque `allowedArchetypes` — é aqui que a população ganha cara.

### 3.3 "Quero um evento novo — por exemplo, tutorial"

**Sem escrever código:**

1. `ChatTopicDatabase` → nova entrada → `id: tutorial.bem_vindo`
2. Escreva as falas, ajuste volume/humor/prioridade.
3. Na cena, no objeto que deve disparar, adicione **`ChatTrigger`**:
   - `topicId`: `tutorial.bem_vindo`
   - `raiseOn`: `OnTriggerEnter` (ou `OnEnable`, ou `Manual`)
   - `once`: ligado
4. Pronto. Nenhuma recompilação.

**Com código, de qualquer lugar do projeto:**

```csharp
Chat.ChatStimulusBus.Raise("tutorial.bem_vindo", 0.5f);
```

Uma linha. Sem referência, sem interface, sem asmdef.

### 3.4 "Quero uma dica nova para quando o jogador travar"

1. `ChatTopicDatabase` → nova entrada `hint.alguma_coisa`:
   - `priority`: 55
   - `ignoreViewerFloor`: **ligado** (a dica precisa chegar mesmo sem audiência)
   - `cooldown`: 30
2. Na cena, adicione **`ChatIdleWatcher`** no objeto que sabe do estado:
   - preencha o `nudge` (topicId, idleSeconds, repeatSeconds, intensity)
3. Chame `Notify()` sempre que houver progresso — dá pra ligar direto num UnityEvent.
4. Chame `Disarm()` quando a etapa acabar.

### 3.5 "Uma fala específica aparece demais"

Três opções, da mais suave pra mais dura:

1. Baixe o `weight` dela.
2. Escreva mais falas para o pool - abaixo de 6 a supressao por recencia nao tem de onde escolher.

### 3.6 "Quero mudar a personalidade de um viewer"

`ViewerPopulation.asset` → o viewer → `archetype` e `chattiness`.

Regra prática: **poucos regulares com chattiness alta é o que faz o chat parecer gente.** Se todo
mundo fala igual, vira gerador de nomes.

### 3.7 Diagnóstico — "o chat não está dizendo nada"

Na ordem:

1. **Console.** O `ChatManager` avisa no start quais pools estão vazios e quais tópicos foram
   disparados sem existir no banco (uma vez por ID, não spamma).
2. **Cena.** O chat só liga na cena `Game`.
3. **Viewers.** Com 0 viewers a curva dá 0 falas/min — é proposital. Confira a barra de audiência.
4. **Referências.** `topicDatabase`, `ambientDatabase` e `viewerPopulation` estão atribuídos?
5. **Pool vazio.** Tópico sem falas fica em silêncio por definição.

---

## 4. Mapa de arquivos

| Arquivo | Assembly | Papel |
|---|---|---|
| `Components/Chat/ChatStimulus.cs` | Components | Barramento, `ChatTopics`, estado de audiência |
| `Components/Chat/ChatNudge.cs` | Components | Cutucada: relogio de "o jogador travou" |
| `Components/Chat/ChatTrigger.cs` | Components | Disparo por inspector |
| `Components/Chat/ChatIdleWatcher.cs` | Components | "Travou" por inspector |
| `Components/TargetChatData.cs` | Components | Pool, sorteio por peso, recência, arquétipo |
| `Components/RecordableIdentifier.cs` | Components | Config por objeto |
| `Components/Health.cs` | Components | `OnAnyHealthChanged` (client-side) |
| `ScriptableObjects/ChatMessageDatabaseSO.cs` | ScriptableObjects | Avistamentos |
| `ScriptableObjects/ChatTopicDatabaseSO.cs` | ScriptableObjects | Acontecimentos |
| `ScriptableObjects/ChatAmbientDatabaseSO.cs` | ScriptableObjects | Fundo |
| `ScriptableObjects/ViewerPopulationSO.cs` | ScriptableObjects | População |
| `Player/Chat/ChatManager.cs` | Player | Tradução estímulo → pool |
| `Player/Chat/ChatDirector.cs` | Player | Fila, ritmo, humor, volume |
| `Player/Chat/ChatUi.cs` | Player | Renderização |
| `ChatEventHub.cs` | Game | **Todos** os eventos do jogo → barramento: viewers, doação, missão, porta, luz |
| `Prefabs/Managers/ChatEventHub.prefab` | — | O hub como objeto de cena. **Precisa estar na `Game.unity`.** |

---

## 5. Pendências

### Precisa ser feito no Unity

1. **Arrastar `ChatEventHub.prefab` para a `Game.unity`.** Ele está em
   `Assets/Prefabs/Managers/`, junto dos outros managers. Posição não importa, é um objeto lógico.

   > Não inseri na cena por você porque o `Library/LastSceneManagerSetup.txt` mostra a `Game.unity`
   > aberta no editor. Editar o arquivo no disco com a cena carregada faz o Unity sobrescrever a
   > mudança no próximo save, sem aviso.
   >
   > **Enquanto ele não estiver na cena, nenhum evento do jogo vira chat** — só os avistamentos.

2. **Atribuir `topicDatabase`** no `ChatManager` (`ChatBar.prefab` dentro do `Player.prefab`).

   | Campo | Asset | Situação |
   |---|---|---|
   | `messageDatabase` | `ChatMessageDatabase.asset` | já cabeado |
   | `ambientDatabase` | `ChatAmbientDatabase.asset` | já cabeado |
   | `viewerPopulation` | `ViewerPopulation.asset` | já cabeado |
   | **`topicDatabase`** | **`ChatTopicDatabase.asset`** | **falta** |

   > O campo antigo chamava-se `stimulusDatabase` e apontava para `ChatStimulusDatabase.asset`.
   > Ambos foram renomeados quando o sistema passou a usar IDs de texto em vez de enum, então o
   > Unity vai descartar o override antigo sozinho. **Sem essa referência, nenhum acontecimento
   > gera chat** — nem reação, nem dica. Os avistamentos continuam funcionando.

3. **Escrever as falas.** Todos os pools novos estão vazios:

   | Asset | Entradas sem falas |
   |---|---|
   | `ChatTopicDatabase` | 14 (4 dicas + 10 reações) |
   | `ChatAmbientDatabase` | 5 (um por humor) |
   | `ChatMessageDatabase` | `Lights` |

4. **`Room` tem 1 fala só.** Como cada avistamento gera de 1 a 3 mensagens, olhar uma sala faz três
   viewers repetirem a mesma frase.

5. **Revisar `allowedArchetypes`** nas 87 falas existentes — todas entraram como `Everyone`.
   Funcionam assim, mas a personalidade da população só aparece quando isso for marcado.

### Decisões de design em aberto

- **`MissionObject` tem 13 falas e zero objetos** com esse `targetType`.
> **Resolvido:** o nome do player agora chega no `{subject}`. Vem de `PlayerInfos.PlayerName`, um
> `NetworkVariable` que o servidor preenche a partir do `UserData` no spawn e que replica pra todos
> os clients — então dá pra nomear o colega que morreu, não só o próprio jogador. `alguem` continua
> como fallback se o nome ainda não tiver replicado.
