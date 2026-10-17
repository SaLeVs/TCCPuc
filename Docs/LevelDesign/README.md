# Level Design

| Documento | Conteúdo |
|---|---|
| [01-Analise-do-Curso.md](01-Analise-do-Curso.md) | As anotações do curso aplicadas ao jogo: métricas, achados medidos no GYM, 5 D's, beat chart. |
| [02-Toolkit-Catalogo.md](02-Toolkit-Catalogo.md) | Todas as peças com código, tamanho e regras (gerado). |
| [03-GYM.md](03-GYM.md) | Como usar a cena GYM: estações, atalhos, HUD. |
| `toolkit/` | `toolkit.json` + miniaturas, para a página visual do toolkit (gerado). |

## Menus no Unity (`Tools/TCC/Level Design/`)

| Menu | Faz |
|---|---|
| **Toolkit (janela)** | Paleta com todas as peças: busca por código/nome, filtros (colisor, bloqueia visão, interativo), notas, arrastar ou duplo-clique para colocar na cena. |
| **Atualizar catálogo do toolkit** | Varre `Assets/Prefabs` de novo. Códigos e notas existentes são mantidos. |
| **Exportar toolkit** | Gera o Markdown e a pasta `toolkit/` deste diretório. |
| **Gerar cena GYM** | Reconstrói `Assets/LevelDesign/Scenes/GYM.unity` com as métricas atuais. |

## Códigos

`PREFIXO-NN`, por categoria: `SAL` salas prontas, `COR` módulos de corredor, `PAR` paredes,
`PIS` pisos, `TET` tetos, `POR` portas, `COB` cobertura alta, `OBS` obstáculos baixos,
`PAL` palco/auditório, `ILU` iluminação, `SIN` sinalização, `DEC` decoração, `ITM` itens,
`MIS` missões/puzzles, `AUD` audiência, `SIS` sistemas e atores.

Os códigos não mudam quando você roda a varredura de novo (a peça é reconhecida pelo GUID);
peças novas ganham o próximo número da categoria.
