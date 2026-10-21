# GYM — cena de teste de métricas

Cena: `Assets/LevelDesign/Scenes/GYM.unity`. Abra e dê **Play** direto nela: não precisa passar
pelo menu nem pelo lobby. Ela sobe um host LAN sozinha e cria o seu jogador no spawn.

Gerada por `Tools/TCC/Level Design/Gerar cena GYM`. Mudou velocidade, stamina ou visão nos
prefabs? Gere de novo e todas as placas se atualizam (ela é reconstruída do zero — mudanças
feitas à mão na cena se perdem).

## Planta

```
 z=33 ┌──────────┬──────────┬──────────┬──────────┬──────────┐
      │ N1 Loop  │ N2 Cober-│ N3 Altu- │ N4 Agach.│ N5 Loop  │
      │ curto    │ tura/vis.│ ras      │ /larguras│ c/ portas│
 z=5  ├───┐  ┌───┴──┐  ┌────┴─┐  ┌─────┴──┐  ┌──┴──┐  ┌────┤
      │   CORREDOR CENTRAL = PISTA MEDIDA (120 m)   ▲spawn    │
 z=-5 ├───┘  └───┬──┘  └────┬─┘  └─────┬──┘  └──┬──┘  └────┤
      │ S1 Portas│ S2 Itens │ S3 Audi- │ S4 Covil │ S5 Escala│
      │          │          │ ção      │ do monst.│ peças    │
 z=-29└──────────┴──────────┴──────────┴──────────┴──────────┘
      x=-60                    x=0                      x=60
```

Chão e paredes têm grade: linha fina = 1 m, grossa = 5 m.

| Estação | Para testar |
|---|---|
| **Corredor / pista** | Linhas azuis = cronômetro (largada, 10…50 m, chegada). Faixas ciano = 1 sprint inteiro, 10 s andando, alcance nominal da visão. Quadrados vermelhos = monstro aparece a 10/20/30 m. |
| **N1 Loop curto** | Bloco 8×8 e pilares 2×2 e 1×1: dá para girar o monstro? |
| **N2 Cobertura e visão** | Monstro vira estátua; muretas de 1,0/1,2/1,5/2,0 m, props reais, caixa layer `Props` × `Walls` e uma porta solta. Ande e veja "te vê" no HUD. |
| **N3 Alturas** | Degraus 0,1–0,5 m, rampas 10–40°, escada até mirante de 2 m com queda, palco de 1,5 m. |
| **N4 Agachar e larguras** | Túneis 1,0–1,8 m de altura; corredores 0,6–3,0 m de largura. |
| **N5 Loop com portas** | Volta de ~80 m com porta dupla e simples: quanto a porta segura o monstro. |
| **S1 Portas** | Todas as portas do toolkit numa parede; o monstro aparece do outro lado. |
| **S2 Itens** | Pilhas, disquete, caixas de missão, totem de lâmpada, objetos de audiência. |
| **S3 Audição** | Monstro de costas; faixas a 5/10/15/20 m. Ande, corra, agache e olhe o *awareness*. |
| **S4 Covil** | Onde o monstro começa (pausado). |
| **S5 Escala** | Peças reais (paredes, estantes, pallets) com código, tamanho e se têm colisor. |

## Atalhos e HUD

| Tecla | Faz |
|---|---|
| F1 | Mostra/esconde o painel |
| F2 | Monstro aparece 10 m atrás de você |
| F3 | Pausa/solta o monstro (começa pausado) |
| F4 | Cura |
| F5 | Volta ao spawn |
| F6 | Monstro volta para o covil |
| F7 | Stamina infinita |
| F8 | Invencível (cura automática) |
| F9 | Cone de visão do monstro (desenhado no alcance **efetivo**) |
| F10 | Reinicia o GYM (use depois de morrer) |

O painel (topo da tela) mostra: sua velocidade e modo, vida, golpes levados, stamina; estado
da IA do monstro, velocidade, distância, se ele te vê, se a distância está **abrindo** ou
**fechando** (m/s), a que distância ele te viu, quanto durou a última perseguição, o alcance de
visão nominal × efetivo, o *awareness*; e o cronômetro com velocidade média de cada trecho.

Sabotagem: o objeto `GYM Bootstrap` tem um `GymAudienceProvider` com um slider de audiência.
Acima de 0,5 o monstro passa a poder sabotar as portas do GYM.

## Como funciona (nenhum script do jogo foi alterado)

| Arquivo | Papel |
|---|---|
| `Gym/Scripts/GymNetworkBootstrap.cs` | Inicializa Unity Services, monta o `NetworkServer` do `HostSingleton`, sobe o host e cria o jogador. Tira só da instância do jogador os componentes de voz (o GYM não tem sessão Vivox). |
| `Gym/Scripts/GymMonsterRemote.cs` | Pausa, posiciona e manda o monstro para casa, mexendo só no `NavMeshAgent` e ligando/desligando o `MonsterBrain`. |
| `Gym/Scripts/GymPad.cs` | Placa de chão que faz uma ação quando você pisa. Copie para criar novas. |
| `Gym/Scripts/GymStopwatch.cs` | Cronômetro das linhas da pista. |
| `Gym/Scripts/GymDebugHud.cs` | Painel, atalhos e cone de visão. |
| `Gym/Scripts/GymAudienceProvider.cs` | Audiência falsa para liberar a sabotagem do monstro. |
| `Gym/Shaders/GymGrid.shader` | Material de blockout com grade em espaço de mundo. |
| `Editor/Gym/*` | O gerador da cena. |

O `NetworkManager` do GYM fica na raiz e **sem scene management** (cena única, só host); com ele
ligado o Netcode pararia todo Play com um diálogo pedindo para pôr a cena no Build Settings.

## Limites conhecidos

- Só host (um jogador). Clientes LAN não foram pensados para esta cena.
- Sem voz: VivoxPlayer, RemoteVoiceFilter e PlayerVoiceIdentity são removidos da instância.
- Caixas de missão e totem de lâmpada não aceitam interação sem o manager da missão.
- Morreu? F10. Não há "reviver".
