# Níveis protótipo (Game1 … Game7)

Sete níveis jogáveis gerados a partir das plantas desenhadas no canvas
(`Docs/LevelDesign/mapas/prototipos/nivel-N.png`). São provisórios, para testar level design: o
jogo normal (cena `Game`) continua igual.

## Como jogar

- **No Editor:** abra `Assets/Scenes/Prototypes/GameN.unity` e dê Play. O jogo começa pelo fluxo
  normal (Bootstrap → menu → Lobby) e o campo do Lobby já vem com o número desse nível.
- **No Lobby (Editor ou build):** só o host vê o campo **"Nível de teste"** no canto superior
  direito. Digite `1` a `7` para ir para `Game1`…`Game7`; vazio = cena `Game` normal. Os outros
  jogadores não fazem nada: o Netcode leva todo mundo para a cena que o host carregar (todos
  precisam da mesma build).

## Como gerar de novo

`Tools/TCC/Protótipos/Gerar nível N` (ou *Gerar todos os níveis*). O gerador lê a imagem; se você
mudar o desenho e salvar por cima do PNG, é só gerar de novo.

Depois de gerar, abra a cena **sozinha** e rode `Tools/TCC/Protótipos/Assar luz e NavMesh do nível
aberto` (a luz e o NavMesh juntam tudo que estiver carregado no Editor).

## Como o desenho vira nível

| No desenho | No nível |
|---|---|
| Caixa preta / azul | Sala no térreo / no 2º andar (+4,5 m, a altura da plataforma do fundo do auditório) |
| Caixa com 2 ligações | Vaga de **sala sorteada** (14,7 m, portas frente + lado). O sorteio põe as 4 missões do contrato e preenche o resto com salas decoradas |
| Caixa com 1 ligação | Banheiro (ou camarim, se estiver embaixo do auditório) |
| Bilheteria / Escape / Auditorium | Os prefabs reais, girados para as portas deles baterem com o desenho |
| Vermelho / verde | Corredor térreo / 2º andar (4 m, peças `SM_Sophisticated_Wall`, piso e teto do projeto) |
| Verde encontrando vermelho | **Escada** (degraus de 0,20 m: o jogador e o monstro sobem) |
| Amarelo | **Duto**: 1,25 m de altura, só agachado, o monstro não passa. Duto longo vira túnel de manutenção (entrada e saída agachado, meio em pé); duto que liga andares desce em degraus |

As posições relativas do desenho são mantidas; as distâncias são refeitas: salas no tamanho real,
espaço vazio comprimido, e todo trecho reto acima de 24 m ganha uma porta dupla no meio (respiro na
fuga — regra dos ~25 m entre saídas).

## Regras do curso aplicadas

- **Linguagem visual / direção:** placas de saída em todo pé de escada (o caminho para cima é o
  caminho da fuga) e nas portas laterais do palco; luz vermelha nos túneis.
- **Posicionamento com propósito:** bancos e caixas quebram os corredores longos e servem de
  obstáculo na perseguição; cartazes e quadros contam onde você está sem texto.
- **Desejo (risco × recompensa):** objetos de audiência em cima de caixas perto das bocas de duto e
  dos banheiros (becos); quadro de 150 de audiência no corredor da bilheteria.
- **Diversidade:** algumas lâmpadas apagadas para quebrar o ritmo; arandelas alternando lados.
- **Alturas:** 2 andares ligados por escadas, auditório e dutos.

## O que foi mudado no jogo (mínimo)

- `Systems/Loader.cs`: `GameSceneOverride` — se o host escolheu um nível, a carga de `Game` vai
  para ele. Vazio = comportamento de sempre.
- `Enums/SceneNames.cs` (novo) + 7 checagens de nome trocadas por `SceneNames.IsGame` (barra de
  audiência, chat, tempo de gravação, música) para funcionarem também em `Game1`…`Game7`.
- Build Settings: as 7 cenas adicionadas **no fim** (índices de sempre intactos).

Todo o resto está em `Assets/Prototypes/` (gerador, variantes de sala com colisores/luz/audiência,
cópias das missões e do contrato) e `Assets/Scenes/Prototypes/`.

## Achados nos prefabs (corrigidos só nos protótipos)

- `Box_Office_Room` tem um **Monster completo** dentro (seria um segundo monstro) e piso/teto que não
  cobrem as paredes de 24,7 m.
- `BaseRoom_Decorated_1..5` e as salas de missão novas não têm colisor de parede; piso na layer Default.
- O grupo `Decoration` do `Escape_Room` decora um corredor de 39 m que só existe na cena Game.
- As salas do centro (auditório, bilheteria, banheiro, camarim) não têm colisores no prefab.
