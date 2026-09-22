# Chat — passo a passo completo

Do estado atual até o chat funcionando por inteiro. Siga em ordem: cada etapa depende da anterior.

Para *onde fica cada campo*, veja [Chat.md](Chat.md). Este arquivo é **o que fazer**, na ordem.

---

## Etapa 0 — Dois ajustes pendentes

Saíram da auditoria. Um minuto no inspector.

### 0.1 Repor o `Icon Appear Chance`

`Assets/Prefabs/Ui/ChatBar.prefab`, componente `ChatUi`. Está em **0.35** porque o rename de `iconChance` descartou o
valor antigo. Se 0.65 era o que você queria, ponha de volta.

### 0.2 Decidir o `Peak Intensity Multiplier`

O código inicializa **4**, o prefab tem **6**. O prefab vence. Escolha um e alinhe os dois — é
quantas vezes mais rápido o chat fala no auge.

---

## Etapa 1 — Teste de fumaça: o que já funciona hoje

**O chat já funciona para avistamentos.** Antes de escrever qualquer coisa, confirme isso — se
falhar aqui, não adianta escrever conteúdo.

1. Abra a cena `Game.unity`
2. Dê Play (como host)
3. Olhe fixamente para outro player, para o palco ou para o monstro por ~2 segundos
4. **Esperado:** aparecem de 1 a 3 mensagens no chat

### Se não aparecer nada

Na ordem:

| Checar | Como |
|---|---|
| Está na cena certa? | O chat só liga na cena chamada `Game` |
| A audiência passou de 1? | Veja a barra de audiência na HUD. Com **0 viewers o chat é mudo de propósito** — olhe para algo que dê audiência primeiro |
| O `ChatBar` está ativo? | Hierarquia → dentro do seu Player → `ChatBar` |
| As 4 databases estão atribuídas? | `ChatManager` no inspector: `messageDatabase`, `topicDatabase`, `ambientDatabase`, `viewerPopulation` |
| O Console reclamou? | O `ChatManager` avisa no start o que está vazio |

> ⚠️ **No começo da partida o chat é mudo.** `startingAudience` é 0 e `minViewersToTalk` é 1: até a
> audiência passar de 1, as falas são enfileiradas mas não saem — e somem depois de 12 segundos
> (`maxLineAge`). O primeiro avistamento que der audiência destrava. Se te incomodar, baixe
> `minViewersToTalk` para 0.

---

## Etapa 2 — Sua primeira mensagem de evento

Hoje **nenhum evento do jogo gera fala**, porque os 14 tópicos estão vazios. Vamos fazer um
funcionar de ponta a ponta, sem esperar o monstro cooperar.

### 2.1 Escrever uma fala

1. Abra `Assets/Scriptable_objects/Chat/ChatTopicDatabase.asset`
2. Ache a entrada `lights.out`
3. Expanda `Data > Messages` e clique `+`
4. Preencha:

| Campo | Valor |
|---|---|
| `Message` | `APAGOU TUDO` |
| `Allowed Archetypes` | `Everyone` |

### 2.2 Disparar na mão

Sem depender do monstro sabotar:

1. Na cena `Game`, crie um GameObject vazio chamado `TESTE_Chat`
2. Adicione o componente **`ChatTrigger`**
3. Configure:

| Campo | Valor |
|---|---|
| `Topic Id` | `lights.out` |
| `Intensity` | 0.9 |
| `Raise On` | `OnEnable` |
| `Once` | ✔ |

4. **Desative o GameObject** (checkbox ao lado do nome)
5. Dê Play, espere a audiência subir, e **ative o GameObject** no inspector

**Esperado:** de 2 a 4 mensagens no chat, de viewers diferentes.

> Esse `ChatTrigger` é a sua ferramenta de teste para qualquer tópico. Troque o `Topic Id` e repita.
> Apague o objeto quando terminar.

### 2.3 Disparar de código

Se preferir, de qualquer script do projeto, sem referência nenhuma:

```csharp
Chat.ChatStimulusBus.Raise("lights.out", 0.9f);
```

---

## Etapa 3 — Escrever o conteúdo

A parte grande. **17 pools vazios.**

> **Cada fala tem 2 campos:** `Message` (o texto) e `Allowed Archetypes` (quem pode dizer). Só isso —
> toda fala elegível tem a mesma chance.

Sugestão de ordem, do que mais aparece para o que menos aparece:

### 3.1 Prioridade alta — o jogador vê muito

| # | Pool | Onde | Quantas falas | Tom |
|---|---|---|---|---|
| 1 | `Idle` | Ambiente | 15–20 | Conversa fiada. É o que preenche o silêncio a maior parte do jogo |
| 2 | `Bored` | Ambiente | 10–15 | Reclamação. Dispara quando a audiência está caindo |
| 3 | `player.died` | Tópico | 10–12 | Pânico e zoeira |
| 4 | `player.teammate_died` | Tópico | 10–12 | Idem |
| 5 | `Lights` | Avistamento | 10–12 | Está vazio e 2 objetos usam |

### 3.2 Prioridade média — momentos marcantes

| # | Pool | Quantas | Tom |
|---|---|---|---|
| 6 | `lights.out` | 8–10 | Susto imediato |
| 7 | `hint.lights_out` | 6–8 | **Dica: mandar ir no gerador** |
| 8 | `donation.received` | 8–10 | Usa `{subject}` = nome do doador |
| 9 | `mission.completed` | 8–10 | Comemoração |
| 10 | `player.hurt` | 6–8 | Preocupação/zoeira |
| 11 | `Panic` | Ambiente | 8–10 | Fundo com o monstro em cena ou perseguindo |

### 3.3 Prioridade baixa — aparece pouco

| # | Pool | Quantas | Observação |
|---|---|---|---|
| 13 | `hint.mission_idle` | 6–8 | Dica de "vai achar uma missão" |
| 14 | `hint.exploration_idle` | 6–8 | Dica de "vai ver outra coisa" |
| 15 | `hint.door_locked` | 6–8 | ⚠️ ver aviso abaixo |
| 17 | `lights.restored` | 5–6 | Alívio |
| 18 | `audience.surge` | 5–6 | "tá bombando" |
| 19 | `audience.drop` | 5–6 | "tão saindo" |
| 20 | `donation.expired` | 5–6 | Decepção |
| 21 | `Room` | Avistamento | +8 | Hoje tem **1 só**, e a rajada é 1–3: repete garantido |

> ⚠️ **`hint.door_locked` só existe por causa do monstro.** As duas únicas coisas que trancam porta
> no jogo são o monstro arrombando e o monstro sabotando. Não existe porta com chave. Escreva no
> tom de *"o monstro tá segurando essa porta, corre"*, nunca *"procura a chave"*.

### 3.4 Regras ao escrever

| Regra | Por quê |
|---|---|
| **Pool com 1 fala repete** | A rajada pede 1–3 falas; com uma só, os três viewers dizem a mesma coisa |
| **Mínimo 6 falas por pool** | Abaixo disso a supressão por recência não tem de onde escolher |
| **`{subject}`** | Vira o nome do doador ou do player morto. Sem valor, vira `alguem` |

---

## Etapa 4 — Dar personalidade ao chat

**Maior ganho por menor esforço, e hoje está 100% sem uso.**

Sua população tem 30 viewers bem divididos — 6 Hype, 5 Scared, 8 Troll, 5 Backseat, 6 Lurker. Mas
**as 87 falas existentes estão todas como `Everyone`**, então qualquer um diz qualquer coisa e a
divisão não aparece no jogo.

1. Abra `ChatMessageDatabase.asset`
2. Percorra as falas e troque `Allowed Archetypes` de `Everyone` para o que fizer sentido:

| Fala do tipo | Marque |
|---|---|
| `"medroso esse seu amigo"` | `Troll` |
| `"vai pra esquerda"` | `Backseat` |
| `"CORRE"` | `Hype`, `Scared` |
| `"não é possível"` | `Scared` |
| `"olha aquilo ali"` | `Everyone` (serve pra qualquer um) |

**Não precisa fazer tudo de uma vez.** Comece pelos 20 alvos de `Monster` — é onde a personalidade
mais aparece, porque é o momento de tensão.

---

## Etapa 5 — Controlar o volume

**Um único controle manda em tudo:** `ChatDirector > Lines Per Minute By Viewers` (componente ao lado do `ChatManager`).

Curva: eixo X é quantos viewers, eixo Y é quantas falas por minuto.

| Quero | Faça |
|---|---|
| Chat mais calado no geral | Baixe todos os pontos Y |
| Chat mudo com pouca gente | Puxe os primeiros pontos para perto de 0 |
| Picos menos explosivos | Baixe `Peak Intensity Multiplier` (não mexa na curva) |
| Reação durar menos | Suba `Intensity Decay Per Second` |
| Menos conversa de fundo | Suba `Seconds Between Lines` do humor no `ChatAmbientDatabase` |
| Sem conversa de fundo | Desmarque `Ambient Enabled` |

### Volume de um evento específico

No `ChatTopicDatabase`, na entrada do tópico:

| Quero | Campo |
|---|---|
| Mais/menos falas por vez | `Min Messages` / `Max Messages` |
| Chat mais agitado depois | `Intensity` |
| Repetir com menos frequência | `Cooldown` — **exceto em dica**, ver abaixo |

### Frequência de uma dica

⚠️ **Dica tem dois freios e é fácil mexer no errado.**

| Controle | Onde | Vale? |
|---|---|---|
| `Repeat Seconds` do nudge | `ChatEventHub` na cena, bloco `Hints` | ✅ **É este** |
| `Cooldown` do tópico | `ChatTopicDatabase` | ❌ Se for menor que o `Repeat Seconds`, não faz nada |

Tempos atuais:

| Dica | Primeira vez | Repete |
|---|---|---|
| Luz apagada | 12s | 25s |
| Missão parada | 90s | 60s |
| Exploração parada | 75s | 50s |

As três no mesmo componente: `ChatEventHub` na cena, bloco `Hints`.

---

## Etapa 6 — Adicionar coisas novas

### 6.1 Um evento novo (ex.: tutorial) — sem código

1. `ChatTopicDatabase` → `+` → `Id`: `tutorial.bem_vindo`
2. Escreva as falas
3. Configure: `Priority` 50, `Ignore Viewer Floor` ✔ (o tutorial tem que aparecer mesmo sem audiência)
4. Na cena, no objeto que dispara, adicione **`ChatTrigger`**:
   - `Topic Id`: `tutorial.bem_vindo`
   - `Raise On`: `OnTriggerEnter` (ou `OnEnable`, ou `Manual` + UnityEvent)
   - `Once`: ✔

Zero recompilação.

### 6.2 Uma dica nova de "travou" — sem código

1. `ChatTopicDatabase` → nova entrada `hint.alguma_coisa`
   - `Priority` 55, `Ignore Viewer Floor` ✔, `Cooldown` 30
2. Na cena, no objeto que sabe do estado, adicione **`ChatIdleWatcher`**
3. Preencha o `Nudge`: `topicId`, `idleSeconds`, `repeatSeconds`, `intensity`
4. Ligue `Notify()` num UnityEvent do que conta como progresso
5. Ligue `Disarm()` em quando a etapa acabar

### 6.3 Um evento novo de um sistema do jogo — com código

1. Abra `Assets/Scripts/ChatEventHub.cs`
2. Assine em `OnEnable`, desassine em `OnDisable`
3. Chame `ChatStimulusBus.Raise("seu.topico", intensidade, assunto)`
4. Cadastre `seu.topico` no `ChatTopicDatabase`

> ⚠️ Antes de assinar um evento, **confira se ele não está atrás de `if (!IsServer) return`**. Se
> estiver, ele só dispara no host e o chat fica mudo nos outros clients. Procure o estado replicado
> equivalente (`NetworkVariable`, `NetworkList`) ou um RPC `SendTo.ClientsAndHost`.

### 6.4 Dica que fica mais direta com o tempo

Não existe campo de escalada — e é de propósito. O jeito que funciona é **dois tópicos**:

```
hint.lights_out           nudge idle 12s    "cadê a luz kkkk"
hint.lights_out_urgente   nudge idle 90s    "VAI NO GERADOR"
```

Aí muda o que está escrito, que é o que o jogador percebe.

---

## Etapa 7 — Quando não aparecer: diagnóstico

Na ordem, do mais comum ao mais raro:

| # | Sintoma | Causa provável |
|---|---|---|
| 1 | Nada, nunca | Não está na cena `Game` |
| 2 | Nada no começo da partida | Audiência ainda em 0 (`minViewersToTalk: 1`) |
| 3 | **Só avistamento funciona** | `ChatEventHub` não está na cena, ou `topicDatabase` não atribuído |
| 4 | Um tópico específico mudo | Pool vazio — o Console avisa no start quais são |
| 5 | Console: `nothing raised 'X'` | Erro de digitação no `Topic Id` de um `ChatTrigger` |
| 6 | Dica não aparece | `Repeat Seconds` do nudge alto demais, ou algo chamando `ReportProgress()` sem parar |
| 8 | Chat repete muito | Pool pequeno demais — menos de 6 falas |
| 9 | Doação sem nome | `fakeDonorNames` do `DonationDefinition` não aponta pro `ViewerPopulation.asset` |

**O Console é seu amigo:** o `ChatManager` avisa uma vez no start quais pools estão vazios, e avisa
uma vez por ID quando alguém dispara um tópico que não existe no banco.

---

## Checklist completo

```
ETAPA 0 — ajustes
[ ] Icon Appear Chance decidido (0.35 ou 0.65)
[ ] Peak Intensity Multiplier alinhado entre código e prefab

ETAPA 1 — funcionando
[ ] Play na cena Game, olhar pra algo, mensagens aparecem
[ ] Console sem erro (warning de pool vazio é esperado)

ETAPA 2 — primeiro evento
[ ] Uma fala escrita em lights.out
[ ] ChatTrigger de teste disparou e apareceu no chat
[ ] Objeto de teste apagado

ETAPA 3 — conteúdo (19 pools)
[ ] Ambiente: Idle, Bored, Panic
[ ] Mortes: player.died, player.teammate_died
[ ] Luz: lights.out, hint.lights_out, lights.restored
[ ] Doação: donation.received, donation.expired
[ ] Missão: mission.completed, hint.mission_idle
[ ] Dano: player.hurt
[ ] Audiência: audience.surge, audience.drop
[ ] Dicas: hint.door_locked, hint.exploration_idle
[ ] Avistamento: Lights (vazio), Room (só 1 fala)

ETAPA 4 — personalidade
[ ] Allowed Archetypes revisado nas 20 falas de Monster
[ ] Allowed Archetypes revisado no resto das 87

ETAPA 5 — volume
[ ] Curva de viewers ajustada ao gosto
[ ] Tempos das dicas conferidos

DECISÕES EM ABERTO
[ ] MissionObject tem 13 falas e 0 objetos — marcar objetos ou realocar as falas?
[ ] minViewersToTalk: 1 deixa o começo mudo — manter ou baixar pra 0?
```
