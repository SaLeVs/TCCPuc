# Level design — o curso aplicado ao TCC

As anotações do curso, lidas contra o que o jogo realmente é hoje. Os números vêm dos prefabs
`Player` e `Monster` (lidos pelo toolkit, ver [02-Toolkit-Catalogo.md](02-Toolkit-Catalogo.md)).
Os comportamentos marcados como **medido no GYM** foram testados em Play na cena
[GYM](03-GYM.md).

---

## 1. O jogo, do ponto de vista de level design

- **Gênero:** terror cooperativo em primeira pessoa, até 4 jogadores. Os jogadores são
  streamers dentro de um teatro/cinema abandonado; audiência, chat e doações reagem ao que a
  câmera deles filma.
- **Loop:** explorar → cumprir missões (caixas/totens, lâmpadas, canos, mesa de som) → juntar
  disquetes → fugir pela porta principal, enquanto um monstro patrulha, investiga barulho,
  persegue, força portas e sabota luzes e portas.
- **Mapa atual (cena Game):** um **hub** de corredores ligando auditório, camarim, depósitos e
  salas de missão sorteadas (`SpawnRooms`). O monstro patrulha 15 setores (`PatrolSector`).
- **Não existe pulo.** Toda a verticalidade vem de degraus, escadas, rampas e quedas.

## 2. As métricas — o "passo 1" do curso

O curso pede para fechar gênero, pulo, velocidade, habilidades e distâncias **antes** de
desenhar. Estes são os valores atuais (ajustados em 2026-10-19: perseguição, rastreio, barulho de corrida, sabotagem e interação):

| Métrica | Valor | O que significa para o mapa |
|---|---|---|
| Andar / correr / agachado | 3 / 6 / 1,5 m/s | 10 s andando = 30 m. Um corredor de 30 m "dura" 10 s. |
| Stamina | 4 s de corrida (24 m) | Exausto, só volta a correr depois de 5,3 s. |
| Altura em pé / agachado | 1,40 m / 1,16 m | Túnel de 1,2–1,3 m só passa agachado. |
| Degrau máximo do jogador | 0,30 m (**só na layer Ground**) | Degrau em outra layer vira parede. |
| Monstro: patrulha / investiga / persegue | 2 / 3,5 / **5** m/s | Correndo você abre 1 m/s (4 m num sprint); andando ele tira 2 m/s. A fuga é pelo mapa: porta, túnel, rampa ou loop a cada ~25 m. |
| Monstro: altura / raio | 1,8 m / 0,35 m | Teto abaixo de 1,8 m ou vão < 0,7 m é rota de fuga. |
| NavMesh do monstro | rampa ≤ 10°, degrau ≤ 0,5 m | Rampa de 20° o jogador sobe, o monstro não. |
| Visão do monstro (configurada) | 40 m, 90° | Ver o achado A abaixo: na prática é bem menos. |
| Rastreio depois de perder de vista | **2 s** | Virar numa bifurcação ou passar uma porta despista de verdade. |
| Audição | 60 m, perde força por parede | Correr faz **3×** o barulho de andar; agachado 0,3×. |
| Ataque | 1 m, 15 de dano, 1 s | 7 golpes zeram a vida. |
| Portas | seguram o monstro ~3 s | Uma porta fechada é um "respiro" de 3 s. Fechar com alguém no caminho empurra a pessoa para o lado dela; quem abre nunca é derrubado pela própria porta. |
| Interação | **1,5 m** | Itens e totens precisam estar a 1,5 m de onde o jogador para. |
| Sabotagem do monstro | a cada **20–40 s** | Luz apagando vira evento, não ruído de fundo. |
| Lanterna | 100 s de bateria, 10 m | Pilhas viram recurso de decisão. |

### Achados medidos no GYM (importantes para balancear)

**A. O monstro enxerga ~5–10 m, não 40 m.** O `VisionSensor` recusa alvos fora de uma faixa
vertical de ±2,5 m medida no espaço do objeto `HeadVision`, que é filho do osso
`mixamorig:Head`. A animação inclina a cabeça 10–25° para cima, e a faixa "sai" do jogador
cedo: parado ele te vê a ~5 m; perseguindo, a 6–10 m. Testado no GYM com o monstro parado
olhando direto para o jogador: vê a 5 m, não vê a 10, 15, 20 e 27 m. O HUD do GYM mostra esse
alcance efetivo ao vivo e o cone de visão (F9) é desenhado com ele.
*Não mexi no script.* Se for bug (e não intenção), as saídas mais simples são: um objeto de
visão que não herde a rotação da cabeça (filho da raiz do monstro), ou fazer o teste de altura
em espaço de mundo. Decidam depois de jogar o GYM com e sem a correção — isso muda todo o
tamanho dos espaços.

**B. Porta fechada não bloqueava a visão — só no prefab.** O prefab `Monster` (usado pelo GYM) não
tinha `Doors` nem `Props` em `occlusionLayers`; a cena Game já sobrescrevia isso com as duas.
Corrigido no prefab em 2026-10-19. A audição tem a mesma diferença: na Game a layer `Doors`
abafa o som, no prefab não — o GYM ainda ouve através de portas.

**C. Paredes e pisos modulares não têm colisor.** `SM_Sophisticated_Wall_*`, `SM_Unique_Floor*`
e a maioria das paredes só têm a malha; a cena Game usa grupos separados (`Coliders Walls`,
`Coliders Floor`). Todo mapa novo precisa repetir isso — o catálogo marca essas peças com ∅.

**D. Itens de missão precisam do manager.** As caixas (`BoxCirclePickable`...) e o
`LampTotem` recusam interação fora de uma missão montada. Pilhas, disquete e portas funcionam
soltos.

**E. Perseguição em números.** Um sprint inteiro abre 4 m (6 − 5 m/s × 4 s). Somado ao achado
A, isso quer dizer que um sprint costuma tirar você da visão; o monstro ainda segue sua posição
real por 2 s ("graça") e só então esfria e vai investigar. Cansado (você andando a 3, ele a
5 m/s), ele fecha 2 m por segundo — por isso o mapa precisa de uma saída a cada ~25 m. O GYM tem a pista e os loops para medir isso.

**F. Avisos que já existem no projeto:** a lista `DefaultNetworkPrefabs` tem uma entrada vazia e
o `PipeConnection` não tem `NetworkObject` (o Netcode avisa em todo Play).

---

## 3. As ferramentas do curso, uma a uma

| Etapa do curso | No TCC |
|---|---|
| **Toolkit** | Catálogo com 325 peças em 16 categorias, cada uma com código (`PAR-16`, `POR-03`...), tamanho e regras. Janela `Tools/TCC/Level Design/Toolkit`. |
| **Gym** | Cena `GYM` gerada a partir das métricas: pista medida, loops, alturas, túneis, larguras, portas, visão, audição. |
| **Sandbox** | Próximo passo: uma cena com uma sala de missão real + monstro + 2 jogadores, para testar o *flow* (não só métricas). |
| **Library** | Campo **notes** de cada peça no catálogo ("essa porta dupla é ótima para fechar loop") e a estação S5 do GYM com as peças reais em escala. |
| **Beat chart** | Ver seção 5. |
| **Block out** | Material de grade do GYM (`Gym_Floor`, `Gym_Wall`...) + cubos; troque pelas peças reais depois, pelo código. |
| **Visual language** | Categoria `SIN` (placas de saída, cartazes) e `ILU` (luzes). Já existe linguagem de cor nas portas (preta/azul/vermelha/saída) — vale dar significado fixo a cada cor. |
| **Gameplay** | Testar no GYM e no Sandbox; o HUD do GYM mede distância, velocidade, quem vê quem. |
| **Story elements** | O jogo já tem gatilhos de narrativa prontos: `ChatTrigger`/`ChatStimulus` (o chat reage), `MusicManager`, doações por TTS. Use como os "placeholders" de cutscene do curso. |
| **Polish** | Iluminação, som (Steam Audio já está no projeto), decoração `DEC`. |

## 4. Os 5 D's no TCC

- **Distances — "smaller & closer".** Com 3 m/s, uma sala de 15 m atravessa em 5 s. O hub da
  Game tem corredores longos (o corredor leste vai de z=7 a z=45, ~38 m = 13 s andando sem nada
  acontecendo). Preencher com bifurcações, portas e cobertura a cada ~10–15 m.
- **Decisions — 2 a 5 opções.** Já existem decisões boas para explorar no mapa:
  inventário de 3 espaços (pilha × disquete × item de missão), filmar objetos de audiência
  (recompensa) perto do monstro (risco), porta fechada (segura 3 s mas faz barulho) × aberta,
  atalho agachado (lento, 1,5 m/s, mas o monstro não passa) × corredor largo (rápido, exposto).
- **Direction.** Óbvio: portas que abrem, onde ficam as missões, a saída (`SM_Exit_Sign`,
  `SM_Exit_Light`). Sutil: atalhos por túnel, salas de loot. Luz guia; o monstro sabota luz —
  a direção pode ser "roubada" de propósito.
- **Desire.** Audiência/doações já fazem o jogador se importar; os objetos `AUD` (coroa, espada,
  máscaras) são iscas naturais.
- **Diversity.** Alternar: corredor tenso → sala de missão (calma, foco no puzzle) → encontro →
  respiro. O beat chart abaixo organiza isso.

## 5. Beat chart sugerido para uma partida (~12–15 min)

| Momento | Intensidade | O que o mapa faz |
|---|---|---|
| Camarim / spawn | 1 | Tutorial sem texto: lanterna, porta, primeira pilha à vista. |
| Hub, primeiro corredor | 2 | Cartazes e luzes apontam as 3 alas; o monstro é só som distante. |
| 1ª missão | 3 → 4 | Sala com 2 saídas; o monstro passa perto pela primeira vez. |
| Primeiro encontro | 7 | Corredor com loop e porta: o jogador aprende a fugir. |
| Respiro | 2 | Sala de loot (pilhas), objeto de audiência como recompensa. |
| 2ª e 3ª missões | 4 → 6 | Mais longe do hub; luzes sabotáveis no caminho. |
| Disquetes / fuga | 8 → 10 | Volta ao hub, porta principal, monstro alertado. |

A curva precisa **subir e descer** — se ficar alta o tempo todo, cansa.

## 6. Três níveis de level design (para as 3 opções de mapa)

- **High level:** do camarim até a porta principal passando por 3 missões.
- **Mid level:** para cada trecho, "o que o jogador sente aqui?" (seção 5).
- **Low level:** onde vai cada porta, luz, placa, pilha — use os códigos do catálogo nas plantas
  (ex.: "POR-05 aqui, COB-09 ×2 na curva, ILU-11 sabotável").

## 7. Próximos passos

1. Jogar o GYM e decidir o achado A (visão) — ele muda o tamanho de tudo.
2. Montar o Sandbox (1 sala de missão real + monstro).
3. Fazer as 3 opções de mapa em blockout, cada uma com beat chart e planta com códigos.
