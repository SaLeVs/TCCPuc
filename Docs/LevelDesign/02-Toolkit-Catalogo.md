# Toolkit de Level Design — catálogo

> Gerado por `Tools/TCC/Level Design/Exportar toolkit` em 2026-10-16 21:17. Não edite à mão: rode o menu de novo.
> Suas anotações ficam no campo **notes** do asset `LevelToolkitCatalog` e saem aqui na coluna Notas.

## Métricas do jogo (lidas dos prefabs)

```
Jogador: anda 3 m/s · corre 6 m/s · agachado 1,5 m/s
Stamina: 4 s de corrida · exausto volta a correr em 5,3 s
Corpo: 1,4 m em pé · 1,16 m agachado · degrau máx. 0,3 m · alcance de interação 1 m
Vida: 100 · lanterna 100 s, alcance 10 m
Monstro: patrulha 2 m/s · investiga 3,5 m/s · persegue 4 m/s
Sentidos: visão 40 m / 90° · audição 60 m · segue sem ver por 4 s
Ataque: 1 m · 15 de dano · 1 s de recarga · 7 golpes zeram a vida
Perseguição: um sprint abre 8 m; andando o monstro tira 1 m/s, alcança em 8 s
Bloqueia visão do monstro: Default, Ground, Interactable, Scenary, DeadPlayer, MainStage, Recordable, Walls
Teto para agachar: Ground, Scenary, Recordable, Walls, Lights, Doors, Props
Degraus sobem em: Ground
Interação (raio): Interactable, Doors
```

## SAL — Salas prontas (20)

Blocos grandes já montados. Bons para o high level: ligar sala a sala pelo hub.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `SAL-01` | LootRoom | Sala de loot | 18,5 × 4,0 × 18,5 m | Default | Colisor, Bloqueia visão, Rede |  |
| `SAL-02` | Mission Room Default | Sala de missão | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-03` | MissionRoomBox | Sala de missão | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-04` | MissionRoomLights | Sala de missão | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-05` | MissionRoomPipes | Sala de missão | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Física, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-06` | MissionRoomSound | Sala de missão | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-07` | Auditorium | Sala especial | 32,5 × 22,4 × 41,5 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Luz, Sabotável, Audiência, Física | O monstro pode sabotar. Dá audiência quando filmado. |
| `SAL-08` | DressingRoom  Game | Sala especial | 10,0 × 3,0 × 10,2 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Precisa NetworkObject, Luz, Sabotável, Obstáculo NavMesh, Faz barulho | Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-09` | DressingRoom Lobby | Sala especial | 10,0 × 3,0 × 10,2 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Luz, Sabotável | O monstro pode sabotar. |
| `SAL-10` | Escape Room | Sala especial | 16,8 × 3,1 × 11,0 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Luz, Sabotável, Física | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-11` | BaseRoom Decorated 1 | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-12` | BaseRoom Decorated 2 | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Audiência, Física, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. Dá audiência quando filmado. |
| `SAL-13` | BaseRoom Decorated 3 | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-14` | BaseRoom Decorated 4 | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-15` | BaseRoom Decorated 5 | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-16` | BaseRoom Default | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |
| `SAL-17` | BaseRoomDefault | Sala fixa | 18,5 × 4,0 × 18,5 m | Default | Colisor, Bloqueia visão, Rede |  |
| `SAL-18` | Bathroom | Sala fixa | 12,7 × 3,0 × 6,6 m | Default | Rede |  |
| `SAL-19` | Box Office Room | Sala fixa | 24,7 × 4,1 × 24,7 m | Default | Colisor, Rede, Sabotável, Audiência | Colisor na layer Default: NÃO bloqueia a visão do monstro. O monstro pode sabotar. Dá audiência quando filmado. |
| `SAL-20` | Dressing Room | Sala fixa | 14,7 × 3,0 × 14,7 m | Default | Colisor, Bloqueia visão, Piso/degrau, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Interagível a até 1 m. O monstro pode sabotar. |

## COR — Módulos de corredor (12)

Trechos de corredor com parede, piso e luz. Medem o ritmo das perseguições.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `COR-01` | Hallway 01 | Corredor | 4,4 × 3,1 × 8,5 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Física |  |
| `COR-02` | Hallway 02 | Corredor | 4,5 × 3,1 × 8,5 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Luz |  |
| `COR-03` | Hallway 03 | Corredor | 4,5 × 3,1 × 8,5 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Luz |  |
| `COR-04` | Hallway 04 | Corredor | 4,4 × 3,1 × 8,5 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Física |  |
| `COR-05` | Hallway 05 | Corredor | 2,7 × 3,2 × 4,0 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Luz, Física |  |
| `COR-06` | Hallway 06 | Corredor | 2,5 × 3,0 × 6,5 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-07` | Hallway 07 | Corredor | 2,5 × 3,0 × 6,5 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-08` | Hallway Collumn Light | Corredor | 4,0 × 3,0 × 4,4 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-09` | Hallway Collumn No Light | Corredor | 4,0 × 3,0 × 4,4 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-10` | Hallway Corner No Light | Corredor | 8,0 × 3,0 × 6,0 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-11` | Hallway Light | Corredor | 4,0 × 3,0 × 4,4 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `COR-12` | Hallway No Light | Corredor | 4,0 × 3,0 × 4,4 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |

## PAR — Paredes e cantos (34)

Estrutura. Malha de 2 m / 4 m. A maioria NÃO tem colisor: some um colisor separado.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `PAR-01` | Wall Bathroom 2m | Banheiro | 0,3 × 3,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-02` | Wall Bathroom 4m | Banheiro | 0,3 × 3,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-03` | Wall Bathroom Corner | Banheiro | 1,3 × 3,0 × 1,3 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-04` | Wall Bathroom Single Door | Banheiro | 2,4 × 3,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-05` | Wall Foyer 2m | Bilheteria | 0,4 × 4,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-06` | Wall Foyer 4m | Bilheteria | 0,4 × 4,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-07` | Wall Foyer Corner | Bilheteria | 2,4 × 4,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-08` | Wall Foyer Double Door | Bilheteria | 4,0 × 4,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-09` | Wall Foyer Single Door | Bilheteria | 4,0 × 4,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-10` | Wall Tickets | Bilheteria | 4,8 × 4,0 × 11,1 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-11` | Smooth Big Wall | Camarim | 0,4 × 3,0 × 4,0 m | Walls | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAR-12` | Smooth Corner Wall | Camarim | 2,0 × 3,0 × 2,0 m | Walls | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAR-13` | Smooth Small Wall | Camarim | 0,4 × 3,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-14` | Hallway Corner Wall | Corredor | 2,0 × 3,0 × 2,0 m | Default |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-15` | Sophisticated Wall 2m | Corredor | 0,4 × 3,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-16` | Sophisticated Wall 4m | Corredor | 0,4 × 3,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-17` | Sophisticated Wall Column 4m | Corredor | 0,5 × 3,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-18` | Sophisticated Wall Corner 1m Left | Corredor | 1,0 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-19` | Sophisticated Wall Corner 1m Right | Corredor | 1,0 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-20` | Sophisticated Wall Corner Camarim Left | Corredor | 2,0 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-21` | Sophisticated Wall Corner Camarim Right | Corredor | 2,0 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-22` | Sophisticated Wall Corner Column 4m | Corredor | 2,5 × 3,0 × 2,5 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-23` | Sophisticated Wall Corner Hallway | Corredor | 2,4 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-24` | Sophisticated Wall Corner Hallway 282 | Corredor | 2,8 × 3,0 × 2,8 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-25` | Sophisticated Wall Corner Hallway Inside | Corredor | 2,5 × 3,0 × 2,5 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-26` | Sophisticated Wall Corner Hallway V2 | Corredor | 2,8 × 3,0 × 2,5 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-27` | Sophisticated Wall Corner Hallway V3 | Corredor | 2,5 × 3,0 × 2,8 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-28` | Sophisticated Wall Corner V1 | Corredor | 2,4 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-29` | Sophisticated Wall Corner V2 | Corredor | 2,4 × 3,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-30` | Sophisticated Wall Double Door 4m V1 | Corredor | 0,4 × 3,0 × 4,0 m | Walls | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAR-31` | Sophisticated Wall Double Door 4m V2 | Corredor | 0,4 × 3,0 × 4,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-32` | Sophisticated Wall Single Door 2m V1 | Corredor | 0,4 × 3,0 × 2,0 m | Walls | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAR-33` | Sophisticated Wall Single Door 2m V2 | Corredor | 0,4 × 3,0 × 2,0 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |
| `PAR-34` | Wall Room Single Door | Corredor | 4,0 × 3,0 × 2,4 m | Walls |  | Sem colisor: o jogador e o monstro atravessam. Some um BoxCollider (layer Walls). |

## PIS — Pisos (11)

Piso visual. Para o jogador andar precisa de colisor na layer Ground.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `PIS-01` | Foyer Floor | Bilheteria | 4,0 × 0,0 × 4,0 m | Default |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-02` | Unique Floor | Camarim | 1,0 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-03` | Unique Floor V2 | Camarim | 1,4 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-04` | Unique Floor V3 | Camarim | 0,4 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-05` | Unique Floor V4 | Camarim | 0,4 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-06` | Floor Hallway | Corredor | 2,4 × 0,0 × 2,0 m | Default |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-07` | Floor Hallway 1 | Corredor | 2,0 × 0,0 × 2,0 m | Default |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-08` | Floor Hallway 2 | Corredor | 1,4 × 0,0 × 2,0 m | Default |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-09` | Unique Floor Hallway | Corredor | 1,0 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-10` | Unique Floor V3 Hallway | Corredor | 0,4 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |
| `PIS-11` | Unique Floor V4 Hallway | Corredor | 0,4 × 0,0 × 1,0 m | Ground |  | Sem colisor: o chão precisa de um BoxCollider separado na layer Ground. |

## TET — Tetos (6)

Fecha o espaço. Só visual; não conta como teto para agachar.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `TET-01` | Roof Bathroom | Banheiro | 1,0 × 0,0 × 1,0 m | Default |  |  |
| `TET-02` | Unique Roof | Camarim | 1,0 × 0,0 × 1,0 m | Default |  |  |
| `TET-03` | Unique Roof V2 | Camarim | 0,4 × 0,0 × 1,0 m | Default |  |  |
| `TET-04` | Roof | Corredor | 2,0 × 0,0 × 2,0 m | Default |  |  |
| `TET-05` | Roof 2 | Corredor | 1,0 × 0,0 × 1,0 m | Default |  |  |
| `TET-06` | Roof 2 | Corredor | 2,0 × 0,0 × 2,4 m | Default |  |  |

## POR — Portas (11)

Decisão e respiro: fecha a visão, o monstro precisa forçar. Precisa de NetworkObject.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `POR-01` | EscapeDoorManager | Managers | 1,8 × 2,1 × 0,2 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Rede | Segura o monstro ~3 s quando ele força. |
| `POR-02` | MainDoorEscape | Managers | 0,9 × 2,1 × 0,2 m | Default | Colisor, Bloqueia visão, Teto p/ agachar, Rede | Segura o monstro ~3 s quando ele força. |
| `POR-03` | Door Black | Portas | 0,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-04` | Door Blue | Portas | 0,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-05` | Door Double Black | Portas | 1,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-06` | Door Double Blue | Portas | 1,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-07` | Door Double Exit | Portas | 1,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-08` | Door Double Red | Portas | 1,9 × 2,1 × 0,1 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-09` | Door Dressing Room | Portas | 0,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Rede, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-10` | Door Exit | Portas | 0,9 × 2,1 × 0,2 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |
| `POR-11` | Door Red | Portas | 0,9 × 2,1 × 0,1 m | Doors | Colisor, Teto p/ agachar, Interativo, Precisa NetworkObject, Sabotável, Obstáculo NavMesh, Faz barulho | Colisor na layer Doors: NÃO bloqueia a visão do monstro. Precisa de NetworkObject: Tools/TCC/Portas/Adicionar NetworkObject nas portas sem rede. Interagível a até 1 m. O monstro pode sabotar. Segura o monstro ~3 s quando ele força. |

## COB — Cobertura alta (14)

Props com colisor mais altos que o jogador em pé: quebram a linha de visão e criam loops de fuga.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `COB-01` | Biombo | Camarim | 2,9 × 2,3 × 0,3 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-02` | BoxConjunto | Camarim | 1,9 × 1,4 × 1,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-03` | Clothes Rack | Camarim | 0,6 × 2,1 × 1,3 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-04` | ConjuntoCardboard | Camarim | 1,6 × 1,4 × 1,4 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-05` | ConjuntoCardboard (1) Variant | Camarim | 1,8 × 1,4 × 1,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-06` | ConjuntoFlightCases | Camarim | 2,5 × 1,6 × 1,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-07` | ConjuntoRack | Camarim | 1,3 × 2,1 × 0,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-08` | Dressing Table | Camarim | 6,2 × 1,5 × 0,7 m | Default | Colisor, Bloqueia visão | Esconde o jogador em pé (1,4 m). |
| `COB-09` | Iron Locker | Depósito | 1,2 × 2,0 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-10` | LockerConjunto | Depósito | 0,6 × 2,4 × 1,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-11` | Metal Shelf V1 | Depósito | 1,6 × 1,8 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-12` | Metal Shelf V2 | Depósito | 1,6 × 1,8 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-13` | ShelfConjunto | Depósito | 0,6 × 2,3 × 1,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `COB-14` | Tripod SoundBox | Palco | 0,9 × 2,5 × 0,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |

## OBS — Obstáculos baixos (25)

Props com colisor mais baixos que o jogador: atrapalham o caminho; os mais altos que o jogador agachado escondem quem agacha.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `OBS-01` | Cardboard Box | Camarim | 0,9 × 0,6 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-02` | Cardboard Small Box | Camarim | 0,5 × 0,4 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-03` | Costume Box | Camarim | 0,9 × 0,5 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-04` | Costume Box Metal | Camarim | 0,9 × 0,5 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-05` | Costume Box Open | Camarim | 0,8 × 0,5 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-06` | Costume Box Open Cover | Camarim | 0,9 × 0,1 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-07` | Costume Box Open Metal | Camarim | 0,8 × 0,5 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-08` | Costume Box Open Metal Cover | Camarim | 0,9 × 0,1 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-09` | Flight Case Big | Camarim | 1,2 × 0,8 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-10` | Flight Case Big Wheels | Camarim | 1,2 × 0,9 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-11` | Flight Case Small | Camarim | 0,6 × 0,8 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-12` | Flight Case Small Wheels | Camarim | 0,6 × 0,9 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-13` | High Stool | Camarim | 0,4 × 1,2 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde só quem está agachado (1,16 m). |
| `OBS-14` | Big Bench | Corredor | 0,7 × 0,5 × 1,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-15` | Small Bench | Corredor | 0,7 × 0,5 × 1,0 m | Props | Colisor, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-16` | Trash Can V1 | Depósito | 0,4 × 0,8 × 0,4 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-17` | Obstacle | Geral | 1,0 × 1,0 × 1,0 m | Default | Colisor, Bloqueia visão | Baixo demais para esconder; atrapalha a rota. |
| `OBS-18` | ConjuntoPallets | Palco | 1,6 × 1,2 × 1,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde só quem está agachado (1,16 m). |
| `OBS-19` | Dolly Cart | Palco | 0,8 × 1,3 × 1,4 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde só quem está agachado (1,16 m). |
| `OBS-20` | Microphone | Palco | 0,3 × 1,5 × 0,3 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde o jogador em pé (1,4 m). |
| `OBS-21` | Pallet V1 | Palco | 1,2 × 0,1 × 1,1 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-22` | Pallet V2 | Palco | 1,2 × 0,2 × 1,1 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-23` | Pallet V3 | Palco | 1,2 × 0,3 × 1,1 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |
| `OBS-24` | SoundBox | Palco | 0,8 × 1,2 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Esconde só quem está agachado (1,16 m). |
| `OBS-25` | SoundBox Floor | Palco | 1,0 × 0,3 × 0,4 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar | Baixo demais para esconder; atrapalha a rota. |

## PAL — Palco e auditório (16)

Peças do palco: dão altura e pontos de interesse (heights & perspectives).

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `PAL-01` | Chair | Auditório | 0,7 × 1,2 × 0,7 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-02` | Chair Closed | Auditório | 0,7 × 1,2 × 0,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-03` | Arch | Palco | 20,0 × 13,4 × 1,2 m | Default | Colisor, Bloqueia visão |  |
| `PAL-04` | Auditorium Stage Wall Big | Palco | 20,0 × 15,0 × 0,5 m | Default |  |  |
| `PAL-05` | Auditorium Top | Palco | 32,4 × 1,6 × 31,5 m | Default |  |  |
| `PAL-06` | Curtain Folded Left | Palco | 7,8 × 13,4 × 3,0 m | Default |  |  |
| `PAL-07` | Curtain Folded Right | Palco | 7,8 × 13,4 × 3,0 m | Default |  |  |
| `PAL-08` | Curtain Left | Palco | 8,8 × 13,5 × 1,9 m | Default |  |  |
| `PAL-09` | Curtain Right | Palco | 8,8 × 13,5 × 1,9 m | Default |  |  |
| `PAL-10` | Layher Tower | Palco | 1,9 × 3,5 × 1,9 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-11` | Stage | Palco | 20,0 × 1,5 × 15,7 m | Default |  |  |
| `PAL-12` | Stage Wall V1 | Palco | 3,0 × 12,0 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-13` | Stage Wall V2 | Palco | 4,0 × 12,0 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-14` | Stage Wall V3 | Palco | 5,0 × 12,0 × 0,5 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `PAL-15` | Truss Curve | Palco | 0,6 × 0,6 × 0,3 m | Default | Colisor, Bloqueia visão |  |
| `PAL-16` | Truss Straight | Palco | 0,3 × 2,0 × 0,3 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |

## ILU — Iluminação (13)

Luz guia o jogador (direction). Luzes com LightSabotage podem ser apagadas pelo monstro.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `ILU-01` | Chandelier | Auditório | 4,9 × 4,7 × 4,9 m | Default |  |  |
| `ILU-02` | Ceiling Light | Camarim | 0,4 × 0,5 × 0,2 m | Default |  |  |
| `ILU-03` | Light Support | Camarim | 0,1 × 0,1 × 2,7 m | Scenary |  |  |
| `ILU-04` | LightRoof | Camarim | 0,4 × 0,6 × 7,9 m | Default | Colisor, Bloqueia visão |  |
| `ILU-05` | LightRoof Variant | Camarim | 0,4 × 0,6 × 9,9 m | Default | Colisor, Bloqueia visão |  |
| `ILU-06` | Straight Ceiling Light | Camarim | 0,4 × 0,5 × 0,2 m | Default | Colisor, Bloqueia visão |  |
| `ILU-07` | Double Light | Corredor | 0,4 × 0,6 × 0,9 m | Scenary |  |  |
| `ILU-08` | Hallway Light Roof | Corredor | 0,5 × 0,8 × 0,5 m | Default |  |  |
| `ILU-09` | Single Light | Corredor | 0,5 × 0,5 × 0,3 m | Props |  |  |
| `ILU-10` | Light Ceiling Rectangular | Depósito | 1,5 × 0,1 × 0,2 m | Scenary |  |  |
| `ILU-11` | SabotageLight | Managers | — | Default | Luz, Sabotável | O monstro pode sabotar. |
| `ILU-12` | Tripod Spotilight V1 | Palco | 0,9 × 1,8 × 0,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `ILU-13` | Tripod Spotilight V2 | Palco | 0,9 × 1,7 × 0,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |

## SIN — Sinalização (linguagem visual) (37)

Placas, cartazes, saídas: dicas que pertencem ao mundo (visual language).

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `SIN-01` | Bathroom Monster Sign | Banheiro | 0,2 × 0,2 × 0,0 m | Default |  |  |
| `SIN-02` | Menu Board Big 1 | Bilheteria | 0,6 × 1,1 × 0,1 m | Default |  |  |
| `SIN-03` | Menu Board Big 2 | Bilheteria | 0,6 × 1,1 × 0,1 m | Default |  |  |
| `SIN-04` | Menu Board Small 1 | Bilheteria | 0,5 × 1,0 × 0,1 m | Default |  |  |
| `SIN-05` | Menu Board Small 2 | Bilheteria | 0,5 × 1,0 × 0,1 m | Default |  |  |
| `SIN-06` | Snacks Sign | Bilheteria | 3,3 × 0,6 × 0,1 m | Default |  |  |
| `SIN-07` | Snacks Sign New | Bilheteria | 3,4 × 0,6 × 0,1 m | Default |  |  |
| `SIN-08` | Tickets Sign | Bilheteria | 3,5 × 0,7 × 0,1 m | Default |  |  |
| `SIN-09` | Tickets Sign New | Bilheteria | 3,7 × 0,6 × 0,1 m | Default |  |  |
| `SIN-10` | Notice Board | Camarim | 0,1 × 1,3 × 2,6 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `SIN-11` | Exit Light | Corredor | 0,4 × 0,2 × 0,0 m | Scenary |  |  |
| `SIN-12` | Exit Sign | Corredor | 0,0 × 0,2 × 0,4 m | Scenary |  |  |
| `SIN-13` | Notice Board | Corredor | 2,6 × 1,3 × 0,1 m | Default |  |  |
| `SIN-14` | Paper 12x16 | Corredor | 0,0 × 0,2 × 0,1 m | Default |  |  |
| `SIN-15` | Paper 12x16 V2 | Corredor | 0,0 × 0,2 × 0,1 m | Default |  |  |
| `SIN-16` | Paper 16x9 | Corredor | 0,0 × 0,1 × 0,2 m | Default |  |  |
| `SIN-17` | Paper 16x9 V2 | Corredor | 0,0 × 0,1 × 0,2 m | Default |  |  |
| `SIN-18` | Paper 25x30 | Corredor | 0,0 × 0,3 × 0,3 m | Default |  |  |
| `SIN-19` | Paper 25x30 V2 | Corredor | 0,0 × 0,3 × 0,3 m | Default |  |  |
| `SIN-20` | Paper 28x40 | Corredor | 0,0 × 0,4 × 0,3 m | Default |  |  |
| `SIN-21` | Paper 28x40 V2 | Corredor | 0,0 × 0,4 × 0,3 m | Default |  |  |
| `SIN-22` | Paper 32x18 | Corredor | 0,0 × 0,2 × 0,3 m | Default |  |  |
| `SIN-23` | Paper 32x18 V2 | Corredor | 0,0 × 0,2 × 0,3 m | Default |  |  |
| `SIN-24` | Paper 45x63 | Corredor | 0,0 × 0,6 × 0,5 m | Default |  |  |
| `SIN-25` | Paper 45x63 V2 | Corredor | 0,0 × 0,6 × 0,5 m | Default |  |  |
| `SIN-26` | Pin V1 | Corredor | 0,0 × 0,0 × 0,0 m | Default |  |  |
| `SIN-27` | Pin V2 | Corredor | 0,0 × 0,0 × 0,0 m | Default |  |  |
| `SIN-28` | Post Its | Corredor | 0,0 × 0,4 × 1,1 m | Default |  |  |
| `SIN-29` | Post Its V2 | Corredor | 0,0 × 0,5 × 1,3 m | Default |  |  |
| `SIN-30` | Poster | Corredor | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-31` | Poster V2 | Corredor | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-32` | Poster | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-33` | Poster 2 | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-34` | Poster 3 | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-35` | Poster 4 | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-36` | Poster 5 | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |
| `SIN-37` | Poster 6 | Missões | 0,0 × 0,8 × 0,6 m | Default |  |  |

## DEC — Decoração (43)

Polish. Preenche espaço vazio; sem efeito de gameplay (sem colisor ou pequeno).

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `DEC-01` | Cup Big | Auditório | 0,2 × 0,3 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar, Física |  |
| `DEC-02` | Cup Small | Auditório | 0,2 × 0,3 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar, Física |  |
| `DEC-03` | Popcorn Paper Big | Auditório | 0,2 × 0,2 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-04` | Popcorn Paper Big Amount | Auditório | 0,2 × 0,3 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-05` | Popcorn Paper Small | Auditório | 0,2 × 0,2 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-06` | Popcorn Paper Small Amount | Auditório | 0,2 × 0,3 × 0,2 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-07` | Bathroom Cabin | Banheiro | 0,3 × 2,0 × 2,0 m | Default |  |  |
| `DEC-08` | Bathroom Door | Banheiro | 1,2 × 1,8 × 0,1 m | Default |  |  |
| `DEC-09` | Bathroom Mirror | Banheiro | 0,7 × 1,0 × 0,0 m | Default |  |  |
| `DEC-10` | Bathroom Mirror 2 | Banheiro | 0,7 × 1,0 × 0,0 m | Default |  |  |
| `DEC-11` | Bathroom Papel Towel Dispenser | Banheiro | 0,3 × 0,5 × 0,1 m | Default |  |  |
| `DEC-12` | Bathroom Soap Dish | Banheiro | 0,2 × 0,3 × 0,1 m | Default |  |  |
| `DEC-13` | Bathroom Trash | Banheiro | 0,6 × 0,6 × 0,5 m | Default |  |  |
| `DEC-14` | Bathroom Trash Can | Banheiro | 0,2 × 0,4 × 0,3 m | Default |  |  |
| `DEC-15` | Sink | Banheiro | 0,7 × 0,6 × 0,6 m | Default |  |  |
| `DEC-16` | Toilet | Banheiro | 0,6 × 1,1 × 0,8 m | Default |  |  |
| `DEC-17` | Toilet Paper Normal 1 | Banheiro | 0,1 × 0,2 × 0,2 m | Default |  |  |
| `DEC-18` | Toilet Paper Normal 2 | Banheiro | 0,1 × 0,1 × 0,1 m | Default |  |  |
| `DEC-19` | Cabinet | Bilheteria | 4,4 × 1,1 × 1,2 m | Default |  |  |
| `DEC-20` | Cabinet New | Bilheteria | 10,5 × 1,1 × 4,5 m | Default |  |  |
| `DEC-21` | Cash Register | Bilheteria | 0,6 × 0,5 × 1,1 m | Default |  |  |
| `DEC-22` | Chair Foyer | Bilheteria | 0,6 × 1,3 × 0,8 m | Default |  |  |
| `DEC-23` | Floor Cabinet | Bilheteria | 3,0 × 1,0 × 1,1 m | Default |  |  |
| `DEC-24` | Floor Cabinet Player | Bilheteria | 3,0 × 1,0 × 1,1 m | Default |  |  |
| `DEC-25` | Popcorn Machine | Bilheteria | 0,7 × 1,0 × 0,6 m | Default |  |  |
| `DEC-26` | Soda Machine | Bilheteria | 0,6 × 0,8 × 0,7 m | Default |  |  |
| `DEC-27` | Sofa | Bilheteria | 2,4 × 1,3 × 1,0 m | Default |  |  |
| `DEC-28` | Stand | Bilheteria | 1,8 × 0,2 × 1,8 m | Default |  |  |
| `DEC-29` | Ticket Counter | Bilheteria | 8,3 × 3,8 × 1,0 m | Default |  |  |
| `DEC-30` | Wall Cabinet | Bilheteria | 2,9 × 0,9 × 0,8 m | Default |  |  |
| `DEC-31` | Frame V2 | Camarim | 0,8 × 1,0 × 0,0 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-32` | Hanger | Camarim | 0,3 × 0,0 × 0,5 m | Scenary |  |  |
| `DEC-33` | Top Hat | Camarim | 0,4 × 0,3 × 0,4 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-34` | Carpet 01 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-35` | Carpet 02 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-36` | Carpet 03 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-37` | Carpet 04 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-38` | Carpet 05 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-39` | Carpet 06 | Corredor | 1,3 × 0,0 × 1,0 m | Default |  |  |
| `DEC-40` | Carpet 07 | Corredor | 1,2 × 0,0 × 1,0 m | Default |  |  |
| `DEC-41` | Frame V1 | Corredor | 0,0 × 1,2 × 0,7 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-42` | Frame V2 | Corredor | 0,0 × 1,0 × 0,8 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |
| `DEC-43` | Mirror | Depósito | 2,0 × 2,5 × 0,0 m | Scenary | Colisor, Bloqueia visão, Teto p/ agachar |  |

## ITM — Itens (17)

Coisas que o jogador pega ou usa: recompensa e decisão (pegar agora ou fugir?).

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `ITM-01` | BoxCirclePickable | Itens | 0,4 × 0,3 × 0,3 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-02` | BoxCircleUsable | Itens | 0,4 × 0,3 × 0,3 m | Default | Interativo, Rede | Interagível a até 1 m. |
| `ITM-03` | BoxCircleVisual | Itens | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `ITM-04` | BoxSquarePickable | Itens | 0,4 × 0,3 × 0,3 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-05` | BoxSquareUsable | Itens | 0,4 × 0,3 × 0,3 m | Default | Interativo, Rede | Interagível a até 1 m. |
| `ITM-06` | BoxSquareVisual | Itens | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `ITM-07` | BoxStarPickable | Itens | 0,4 × 0,3 × 0,3 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-08` | BoxStarUsable | Itens | 0,4 × 0,3 × 0,3 m | Default | Interativo, Rede | Interagível a até 1 m. |
| `ITM-09` | BoxStarVisual | Itens | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `ITM-10` | BoxTrianglePickable | Itens | 0,4 × 0,3 × 0,3 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-11` | BoxTriangleUsable | Itens | 0,4 × 0,3 × 0,3 m | Default | Interativo, Rede | Interagível a até 1 m. |
| `ITM-12` | BoxTriangleVisual | Itens | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `ITM-13` | Disket | Itens | 0,2 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-14` | FlashlightBatteryPickable | Itens | 0,0 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-15` | FlashlightUsable | Itens | 0,0 × 0,1 × 0,0 m | Default | Interativo, Rede | Interagível a até 1 m. |
| `ITM-16` | ItemPickableBase | Itens | 1,0 × 1,0 × 1,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `ITM-17` | ItemUsableBase | Itens | 1,0 × 1,0 × 1,0 m | Default | Rede |  |

## MIS — Missões e puzzles (39)

Objetivos. Totens, peças e managers de puzzle.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `MIS-01` | EletricCircuit | Itens | 0,6 × 0,4 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-02` | EscapeTrigger | Managers | — | Default | Rede |  |
| `MIS-03` | FloppyDiskManager | Managers | 0,3 × 0,1 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-04` | ReadyBoard | Managers | 2,6 × 1,3 × 0,1 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-05` | TotemReady | Managers | — | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-06` | Battery | Missões | 0,0 × 0,1 × 0,0 m | Default |  |  |
| `MIS-07` | Cassette Recorder | Missões | 0,2 × 0,0 × 0,4 m | Default |  |  |
| `MIS-08` | Cassette Tape | Missões | 0,2 × 0,1 × 0,0 m | Default |  |  |
| `MIS-09` | LampsManager | Missões | 0,0 × 0,2 × 0,4 m | Default | Rede, Luz |  |
| `MIS-10` | LampTotem | Missões | 0,2 × 2,1 × 1,7 m | Interactable | Colisor, Bloqueia visão, Teto p/ agachar, Interativo, Rede, Luz | Interagível a até 1 m. |
| `MIS-11` | MissionLaerte | Missões | — | Default | Rede |  |
| `MIS-12` | MissionTotemGroup | Missões | — | Default | Rede |  |
| `MIS-13` | Pipe Big | Missões | 0,2 × 0,4 × 0,2 m | Default |  |  |
| `MIS-14` | Pipe L | Missões | 0,2 × 0,2 × 0,2 m | Default |  |  |
| `MIS-15` | Pipe Small | Missões | 0,2 × 0,2 × 0,2 m | Default |  |  |
| `MIS-16` | Pipe Triple | Missões | 0,2 × 0,2 × 0,2 m | Default |  |  |
| `MIS-17` | PipeBigStraight | Missões | 0,2 × 0,4 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-18` | PipeConnection | Missões | 0,2 × 0,2 × 0,2 m | Default |  |  |
| `MIS-19` | PipeConnectionTriple | Missões | 0,2 × 0,2 × 0,2 m | Default |  |  |
| `MIS-20` | PipeJoint | Missões | 0,2 × 0,2 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-21` | PipeJointWall | Missões | 0,2 × 0,2 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-22` | PipeSmallStraight | Missões | 0,2 × 0,2 × 0,2 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-23` | PipesManager | Missões | — | Default | Rede |  |
| `MIS-24` | Puzzle Box V1 | Missões | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `MIS-25` | Puzzle Box V1 Post It | Missões | 0,0 × 0,1 × 0,1 m | Default |  |  |
| `MIS-26` | Puzzle Box V2 | Missões | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `MIS-27` | Puzzle Box V2 Post It | Missões | 0,0 × 0,1 × 0,1 m | Default |  |  |
| `MIS-28` | Puzzle Box V3 | Missões | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `MIS-29` | Puzzle Box V3 Post It | Missões | 0,0 × 0,1 × 0,1 m | Default |  |  |
| `MIS-30` | Puzzle Box V4 | Missões | 0,4 × 0,3 × 0,3 m | Default |  |  |
| `MIS-31` | Puzzle Box V4 Post It | Missões | 0,0 × 0,1 × 0,1 m | Default |  |  |
| `MIS-32` | Sound Table | Missões | 1,2 × 0,3 × 0,6 m | Default | Colisor, Bloqueia visão |  |
| `MIS-33` | SoundManager | Missões | 1,2 × 0,3 × 0,6 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-34` | Tape Box | Missões | 0,3 × 0,1 × 0,2 m | Default |  |  |
| `MIS-35` | TotemBoxCircle | Missões | 0,1 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-36` | TotemBoxSquare | Missões | 0,1 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-37` | TotemBoxStar | Missões | 0,1 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-38` | TotemBoxTriangle | Missões | 0,1 × 0,1 × 0,0 m | Interactable | Colisor, Bloqueia visão, Interativo, Rede | Interagível a até 1 m. |
| `MIS-39` | Electric Circuit | Palco | 0,6 × 0,4 × 0,2 m | Default |  |  |

## AUD — Audiência (graváveis) (12)

Objetos que dão audiência quando filmados: iscas de risco x recompensa.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `AUD-01` | Crown | Audiência | 0,4 × 0,2 × 0,4 m | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-02` | Happy Mask Low | Audiência | 0,2 × 0,3 × 0,2 m | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-03` | MainStage | Audiência | — | Recordable | Audiência | Dá audiência quando filmado. |
| `AUD-04` | ObjectAudience1 | Audiência | — | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-05` | ObjectAudience3 | Audiência | — | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-06` | ObjectAudience6 | Audiência | — | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-07` | ObjectAudience7 | Audiência | — | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-08` | Sad Mask Low | Audiência | 0,2 × 0,3 × 0,2 m | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-09` | Sophisticated Frame | Audiência | 1,3 × 2,2 × 0,1 m | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-10` | Sword | Audiência | 0,1 × 1,2 × 0,3 m | Recordable | Colisor, Bloqueia visão, Teto p/ agachar, Audiência | Dá audiência quando filmado. |
| `AUD-11` | Toilet Paper Golden 1 | Audiência | 0,1 × 0,2 × 0,2 m | Default |  |  |
| `AUD-12` | Toilet Paper Golden 2 | Audiência | 0,1 × 0,1 × 0,1 m | Default |  |  |

## SIS — Sistemas e atores (15)

Managers, monstro, setores de patrulha e som. Não aparecem, mas fazem o nível funcionar.

| Código | Peça | Tema | Tamanho (L×A×P) | Layer | Regras | Notas |
|---|---|---|---|---|---|---|
| `SIS-01` | AudienceManager | Managers | — | Default | Rede |  |
| `SIS-02` | ChatEventHub | Managers | — | Default |  |  |
| `SIS-03` | DonationAudioManager | Managers | — | Default |  |  |
| `SIS-04` | DonationManager | Managers | — | Default | Rede |  |
| `SIS-05` | EletricCircuitManager | Managers | — | Default | Rede |  |
| `SIS-06` | LostItemsSpawns | Managers | — | Default |  |  |
| `SIS-07` | MissionManager | Managers | — | Default | Rede |  |
| `SIS-08` | MissionRegisterManager | Managers | — | Default |  |  |
| `SIS-09` | SectorStage | Managers | — | Default |  |  |
| `SIS-10` | SpawnManager | Managers | — | Default | Rede |  |
| `SIS-11` | SpawnRoomManager | Managers | — | Default | Rede |  |
| `SIS-12` | Monster | Monstro | 1,2 × 1,8 × 0,9 m | Monster | Colisor, Rede, Sabotável, Audiência | Colisor na layer Monster: NÃO bloqueia a visão do monstro. O monstro pode sabotar. Dá audiência quando filmado. |
| `SIS-13` | MusicManager | Som | — | Default |  |  |
| `SIS-14` | SfxManager | Som | — | Default | Rede |  |
| `SIS-15` | VoiceZoneTrigger | Som | — | Default |  |  |

