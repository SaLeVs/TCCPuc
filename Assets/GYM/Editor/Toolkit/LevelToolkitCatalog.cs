using System;
using System.Collections.Generic;
using UnityEngine;

namespace LevelDesign.Toolkit
{
    /// <summary>
    /// What a piece is for when you are building a level. The two-or-three letter prefix of
    /// each enum is the prefix of the codes it hands out (PAR-04, POR-02...).
    /// </summary>
    public enum ToolkitCategory
    {
        SAL_SalaPronta,
        COR_ModuloCorredor,
        PAR_Parede,
        PIS_Piso,
        TET_Teto,
        POR_Porta,
        COB_CoberturaAlta,
        OBS_ObstaculoBaixo,
        PAL_PalcoAuditorio,
        ILU_Iluminacao,
        SIN_Sinalizacao,
        DEC_Decoracao,
        ITM_Item,
        MIS_MissaoPuzzle,
        AUD_Audiencia,
        SIS_Sistema,
    }

    /// <summary>Game rules a piece carries, read from its components and layers.</summary>
    [Flags]
    public enum ToolkitFlags
    {
        None = 0,
        HasCollider = 1 << 0,
        BlocksMonsterVision = 1 << 1,
        StepClimbable = 1 << 2,
        CrouchCeiling = 1 << 3,
        Interactive = 1 << 4,
        Networked = 1 << 5,
        NeedsNetworkParent = 1 << 6,
        EmitsLight = 1 << 7,
        Sabotable = 1 << 8,
        Recordable = 1 << 9,
        Physics = 1 << 10,
        NavMeshObstacle = 1 << 11,
        MakesNoise = 1 << 12,
    }

    [Serializable]
    public class ToolkitEntry
    {
        public string code;
        public string displayName;
        public ToolkitCategory category;
        public string theme;
        public GameObject prefab;
        public string guid;
        public string assetPath;
        public Vector3 size;
        public string layer;
        public ToolkitFlags flags;
        public string components;

        [Tooltip("Gerado pelo scanner a partir das regras do jogo.")]
        public string autoNotes;

        [Tooltip("Suas anotações (Library do curso). Nunca são sobrescritas pelo scanner.")]
        [TextArea(2, 6)] public string notes;

        [Tooltip("Marque para o scanner não reclassificar esta peça.")]
        public bool lockCategory;

        public bool Has(ToolkitFlags flag) => (flags & flag) == flag;

        public string Prefix => CategoryInfo.Prefix(category);
    }

    [CreateAssetMenu(menuName = "TCC/Level Design/Toolkit Catalog", fileName = "LevelToolkitCatalog")]
    public class LevelToolkitCatalog : ScriptableObject
    {
        [Tooltip("Velocidades, alturas e máscaras lidas dos prefabs Player e Monster na última varredura.")]
        [TextArea(4, 20)] public string metricsSnapshot;

        public string lastScan;

        public List<ToolkitEntry> entries = new();

        public ToolkitEntry FindByCode(string code) => entries.Find(e => string.Equals(e.code, code, StringComparison.OrdinalIgnoreCase));

        public ToolkitEntry FindByGuid(string guid) => entries.Find(e => e.guid == guid);
    }

    public static class CategoryInfo
    {
        public static string Prefix(ToolkitCategory category) => category.ToString().Substring(0, 3);

        public static string Label(ToolkitCategory category) => category switch
        {
            ToolkitCategory.SAL_SalaPronta => "Salas prontas",
            ToolkitCategory.COR_ModuloCorredor => "Módulos de corredor",
            ToolkitCategory.PAR_Parede => "Paredes e cantos",
            ToolkitCategory.PIS_Piso => "Pisos",
            ToolkitCategory.TET_Teto => "Tetos",
            ToolkitCategory.POR_Porta => "Portas",
            ToolkitCategory.COB_CoberturaAlta => "Cobertura alta",
            ToolkitCategory.OBS_ObstaculoBaixo => "Obstáculos baixos",
            ToolkitCategory.PAL_PalcoAuditorio => "Palco e auditório",
            ToolkitCategory.ILU_Iluminacao => "Iluminação",
            ToolkitCategory.SIN_Sinalizacao => "Sinalização (linguagem visual)",
            ToolkitCategory.DEC_Decoracao => "Decoração",
            ToolkitCategory.ITM_Item => "Itens",
            ToolkitCategory.MIS_MissaoPuzzle => "Missões e puzzles",
            ToolkitCategory.AUD_Audiencia => "Audiência (graváveis)",
            ToolkitCategory.SIS_Sistema => "Sistemas e atores",
            _ => category.ToString(),
        };

        public static string Purpose(ToolkitCategory category) => category switch
        {
            ToolkitCategory.SAL_SalaPronta => "Blocos grandes já montados. Bons para o high level: ligar sala a sala pelo hub.",
            ToolkitCategory.COR_ModuloCorredor => "Trechos de corredor com parede, piso e luz. Medem o ritmo das perseguições.",
            ToolkitCategory.PAR_Parede => "Estrutura. Malha de 2 m / 4 m. A maioria NÃO tem colisor: some um colisor separado.",
            ToolkitCategory.PIS_Piso => "Piso visual. Para o jogador andar precisa de colisor na layer Ground.",
            ToolkitCategory.TET_Teto => "Fecha o espaço. Só visual; não conta como teto para agachar.",
            ToolkitCategory.POR_Porta => "Decisão e respiro: fecha a visão, o monstro precisa forçar. Precisa de NetworkObject.",
            ToolkitCategory.COB_CoberturaAlta => "Props com colisor mais altos que o jogador em pé: quebram a linha de visão e criam loops de fuga.",
            ToolkitCategory.OBS_ObstaculoBaixo => "Props com colisor mais baixos que o jogador: atrapalham o caminho; os mais altos que o jogador agachado escondem quem agacha.",
            ToolkitCategory.PAL_PalcoAuditorio => "Peças do palco: dão altura e pontos de interesse (heights & perspectives).",
            ToolkitCategory.ILU_Iluminacao => "Luz guia o jogador (direction). Luzes com LightSabotage podem ser apagadas pelo monstro.",
            ToolkitCategory.SIN_Sinalizacao => "Placas, cartazes, saídas: dicas que pertencem ao mundo (visual language).",
            ToolkitCategory.DEC_Decoracao => "Polish. Preenche espaço vazio; sem efeito de gameplay (sem colisor ou pequeno).",
            ToolkitCategory.ITM_Item => "Coisas que o jogador pega ou usa: recompensa e decisão (pegar agora ou fugir?).",
            ToolkitCategory.MIS_MissaoPuzzle => "Objetivos. Totens, peças e managers de puzzle.",
            ToolkitCategory.AUD_Audiencia => "Objetos que dão audiência quando filmados: iscas de risco x recompensa.",
            ToolkitCategory.SIS_Sistema => "Managers, monstro, setores de patrulha e som. Não aparecem, mas fazem o nível funcionar.",
            _ => string.Empty,
        };

        public static readonly ToolkitCategory[] All = (ToolkitCategory[])Enum.GetValues(typeof(ToolkitCategory));
    }
}
