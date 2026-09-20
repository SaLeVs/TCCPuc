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
   ChatStimulusBus ◄──┼─ ChatAudienceBridge ──┤  (viewers, pico, queda)
   (barramento)       ├─ ChatMissionBridge ───┤  (doação, missão, dica de missão)
                      ├─ ChatDoorBridge ──────┤  (porta trancada)
                      ├─ ChatSabotageBridge ──┤  (luz apagou, dica do gerador)
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
| `messageDatabase` | Falas de **olhar coisas** (por `RecordableTarget`). |
| `topicDatabase` | Falas de **acontecimentos** (por ID de texto). |
| `ambientDatabase` | Conversa de fundo por humor. |
| `viewerPopulation` | Quem está assistindo. |
| `minMessages` / `maxMessages` | Quantas falas cada avistamento gera (1 a 3). |
| `sightingIntensity` | Quanto o chat se agita ao olhar algo comum (0.25). |
| `monsterSightingIntensity` | Idem, pro monstro (0.8) — é isso que deixa a sala tensa. |
| `pendingViewMemory` | Quanto tempo guarda progresso de visão pela metade (12s). |
| `hurtThreshold` | Fração da vida perdida de uma vez pra virar assunto (0.08). |
| `noveltyWindow` | Quanto tempo até um alvo contar como "novo" de novo (120s). |
| `explorationNudge` | A cutucada de "vai explorar". Ver §2.7. |
| `director` | Bloco aninhado, abaixo. |

### 2.2 `ChatDirector` — bloco dentro do `ChatManager`

**O controle mais importante do sistema inteiro:**

| Campo | O que faz |
|---|---|
| **`linesPerMinuteByViewers`** | **Curva: X = nº de viewers, Y = falas por minuto.** É o dial mestre de volume. |
| `peakIntensityMultiplier` | Quantas vezes mais rápido no auge (6x). Multiplica o que a curva deu. |
| `minViewersToTalk` | Viewers mínimos pro chat abrir a boca (1). Abaixo disso só passa dica. |
| `minGap` / `maxGap` | Piso e teto do intervalo entre falas (0.35s / 20s). |
| `intensityDecayPerSecond` | Quão rápido a agitação baixa (0.12 ≈ 8s pra voltar ao normal). |
| `ambientEnabled` | Liga a conversa de fundo. Desligar deixa o chat mudo quando nada acontece. |
| `spamWaveThreshold` | Intensidade pra virar onda de spam (0.75). |
| `spamWaveMin` / `Max` / `Gap` | Tamanho e velocidade da onda (3–6 falas, 0.12s entre elas). |
| `maxQueued` | Falas esperando na fila (14). Passou disso, abre espaço descartando a **mais antiga da faixa de menor prioridade**. |
| `maxLineAge` | Segundos que uma fala pode esperar antes de ser descartada (12s). |
| `speakerAttempts` | Tentativas de achar um viewer com a personalidade certa (4). |

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
| **Lotação** (`TryMakeRoom`) | A fila chega em `maxQueued` | **Prioridade escolhe a faixa, idade escolhe quem.** A vítima é a mais antiga da faixa de menor prioridade. Se a fala nova for mais fraca que toda a fila, ela é que não entra. |

A divisão é intencional: prioridade serve pra **uma enxurrada de conversa fiada não expulsar uma
dica**, não pra dar sobrevida a quem já está velho. Por isso, dentro da mesma faixa, quem sai é
sempre a mais antiga — fila cheia continua reagindo ao *agora* em vez de guardar coisa velha e
recusar coisa fresca.

### 2.3 `ChatUi` — mesmo prefab

| Campo | O que faz |
|---|---|
| `activeMessagesCount` | Linhas visíveis na tela (9). |
| `messageFormat` | Template. Placeholders: `{icon}`, `{color}`, `{viewer}`, `{message}`. |
| `iconChance` | Chance de um viewer ter badge (0.65). |
| `iconPerViewer` | Ligado: cada viewer tem sempre o mesmo ícone. |
| `iconPool` | Índices de sprite permitidos. Vazio = todos. |
| `nameColors` | Paleta de cores dos nomes. |

### 2.4 `ChatMessageDatabase.asset` — falas de avistamento

Por `RecordableTarget`. Cada entrada:

- `messages[]` — a lista de falas
- `recencyWindow` — segundos pra uma fala recuperar o peso total depois de dita (45s)
- `recencyFloor` — pra quanto o peso cai na hora que é dita (0.1 = 10%)

Cada fala (`ChatMessage`):

| Campo | O que faz |
|---|---|
| `message` | O texto. Pode usar `{subject}`. |
| `weight` | Chance relativa dentro do pool. **0 desabilita.** |
| `allowedArchetypes` | Quais personalidades podem dizer isso (flags). |
| `spammable` | Curta o bastante pra virar onda de spam. |

### 2.5 `ChatTopicDatabase.asset` — falas de acontecimento

Por ID de texto. Além do `data` (igual acima):

| Campo | O que faz |
|---|---|
| `id` | O identificador. Ex.: `hint.door_locked`, `tutorial.bem_vindo`. |
| `minMessages` / `maxMessages` | Volume da reação. |
| `intensity` | **Teto** de agitação do tópico. O emissor escala isso. |
| `mood` | Humor que empurra na sala. |
| `cooldown` | Segundos até o tópico poder repetir. É o freio de quem emite repetido. |
| `priority` | Quem fura fila. Ver tabela abaixo. |
| `allowSpamWave` | Permite onda de spam. |
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
| `Idle` | Nada acontecendo, audiência saudável | 9s |
| `Bored` | Audiência caindo (`IsDecaying`) | 7s — chat entediado reclama **mais** |
| `Hype` | Algo bom aconteceu | 5s |
| `Tense` | Perigo por perto | 6s |
| `Panic` | Aconteceu | 4s |

### 2.7 `ChatNudge` — a cutucada com escalada

Usado em três lugares: exploração (no `ChatManager`), missão (na ponte de missões) e luz (na ponte
de sabotagem). Mesmos campos nos três:

| Campo | O que faz |
|---|---|
| `topicId` | Tópico que dispara. |
| `idleSeconds` | Segundos de nada até a primeira cutucada. |
| `repeatSeconds` | Intervalo das repetições. **0 = cutuca uma vez só.** |
| `escalate` | Aumenta a intensidade a cada cutucada ignorada. |
| `startIntensity` / `maxIntensity` | Faixa da escalada. |
| `escalationSteps` | Cutucadas até chegar no máximo (3). |

**Presets atuais** (as pontes nascem em runtime, então os valores vêm do código):

| Dica | idle | repeat | intensidade |
|---|---|---|---|
| Exploração | 75s | 50s | 0.30 → 0.65 |
| Missão | 90s | 60s | 0.30 → 0.70 |
| Luz apagada | 12s | 25s | 0.40 → 0.85 |

> A de exploração fica no `ChatManager`, que está num prefab — essa dá pra editar no inspector. As
> outras duas estão em `ChatMissionBridge.cs:28` e `ChatSabotageBridge.cs:27`.

### 2.8 `ViewerPopulation.asset` — quem assiste

| Campo | O que faz |
|---|---|
| `viewers[].name` | Nome que aparece. |
| `viewers[].archetype` | Personalidade (uma só por viewer). |
| `viewers[].chattiness` | Quanto fala. **É o que separa regular de figurante.** |
| `recentMemory` | Quantos últimos falantes são penalizados (5). |
| `recentPenalty` | Multiplicador de quem falou há pouco (0.15). |

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

### 2.10 Pontes — criam a si mesmas, não precisam de cabeamento

| Ponte | Assembly | Constantes |
|---|---|---|
| `ChatAudienceBridge` | Audience | `NoticeableChange` 12, `BigChange` 60 viewers |
| `ChatMissionBridge` | Missions | `BigDonation` 100 |
| `ChatDoorBridge` | Objects | `CommentRange` 14m |
| `ChatSabotageBridge` | Monster | poll 0.5s (2s fora de partida) |

Elas moram no assembly de origem justamente porque cada uma precisa enxergar tipos que o `Player`
não pode referenciar.

---

## 3. Passo a passo

### 3.1 "O chat fala demais / de menos"

**Não mexa em `minGap`/`maxGap`.** Vá na curva.

1. `Player.prefab` → `ChatBar` → `ChatManager` → `Director` → `Lines Per Minute By Viewers`
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
5. Se for curta tipo `pega pega pega`, marque `spammable`.

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
   - preencha o `nudge` (topicId, idleSeconds, repeatSeconds, escalada)
3. Chame `Notify()` sempre que houver progresso — dá pra ligar direto num UnityEvent.
4. Chame `Disarm()` quando a etapa acabar.

### 3.5 "Uma fala específica aparece demais"

Três opções, da mais suave pra mais dura:

1. Baixe o `weight` dela.
2. Aumente o `recencyWindow` do pool (demora mais pra ela voltar a valer o peso cheio).
3. Baixe o `recencyFloor` (ela despenca mais quando é dita).

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

### 3.8 "Quero que uma fala vire onda de spam"

Precisa das três coisas juntas:

1. A fala marcada como `spammable`.
2. O tópico com `allowSpamWave` ligado.
3. A intensidade do momento passando de `spamWaveThreshold` (0.75).

---

## 4. Mapa de arquivos

| Arquivo | Assembly | Papel |
|---|---|---|
| `Components/Chat/ChatStimulus.cs` | Components | Barramento, `ChatTopics`, estado de audiência |
| `Components/Chat/ChatNudge.cs` | Components | Cutucada com escalada |
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
| `Audience/ChatAudienceBridge.cs` | Audience | Viewers e variação |
| `Missions/ChatMissionBridge.cs` | Missions | Doação, missão, dica de missão |
| `Objects/ChatDoorBridge.cs` | Objects | Porta trancada |
| `Monster/ChatSabotageBridge.cs` | Monster | Luz apagada, dica do gerador |

---

## 5. Pendências

### Precisa ser feito no Unity

1. **Atribuir `topicDatabase`** no `ChatManager` (`ChatBar.prefab` dentro do `Player.prefab`).

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

2. **Escrever as falas.** Todos os pools novos estão vazios:

   | Asset | Entradas sem falas |
   |---|---|
   | `ChatTopicDatabase` | 13 (4 dicas + 9 reações) |
   | `ChatAmbientDatabase` | 5 (um por humor) |
   | `ChatMessageDatabase` | `Lights` |

3. **`Room` tem 1 fala só.** Como cada avistamento gera de 1 a 3 mensagens, olhar uma sala faz três
   viewers repetirem a mesma frase.

4. **Revisar `allowedArchetypes`** nas 87 falas existentes — todas entraram como `Everyone`.
   Funcionam assim, mas a personalidade da população só aparece quando isso for marcado.

### Decisões de design em aberto

- **`MissionObject` tem 13 falas e zero objetos** com esse `targetType`.
- **Nome do player morto** não chega no `{subject}`: o único nome alcançável de `Components` é o do
  GameObject (`Player(Clone)`). Passa `alguem` como fallback.
