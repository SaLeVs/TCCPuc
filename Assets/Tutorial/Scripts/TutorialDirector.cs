using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Audience;
using Components;
using Missions;
using Missions.Donations;
using Missions.Puzzles;
using Monster;
using Network;
using Objects;
using Objects.PickupItems;
using Player;
using Player.Chat;
using ScriptableObjects;
using Systems;
using Ui;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tutorial
{
    /// <summary>
    /// Conduz o tutorial do começo ao fim, falando pelo chat do jogo.
    ///
    /// <para>Cada lição é uma corrotina: a moderadora explica, a faixa do topo diz o que fazer, e o
    /// diretor espera o jogo de verdade confirmar (a lanterna ligou, a caixa entrou no suporte, a
    /// doação completou). Se o jogador empacar, o chat dá dicas - primeiro os viewers, depois a
    /// moderadora, cada vez mais diretas. Nada aqui imita um sistema: são as peças do jogo, ligadas
    /// por fora, e nenhum script do jogo foi alterado.</para>
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        private const int TotalSteps = 12;
        private const string Moderator = "LiaModeradora";

        // Zonas (TutorialZone) e portões (TutorialGate) que o gerador da cena cria com estes nomes.
        public const string ZoneWalkMark = "walk_mark";
        public const string ZoneRunEnd = "run_end";
        public const string ZoneCrouchBeam = "crouch_beam";
        public const string ZoneCrouchTable = "crouch_table";
        public const string ZoneDarkRoom = "dark_room";
        public const string ZoneDarkExit = "dark_exit";
        public const string ZoneDoorsAfter = "doors_after";
        public const string ZonePuzzleRoom = "puzzle_room";
        public const string ZoneStageRoom = "stage_room";
        public const string ZoneFugaStart = "fuga_start";
        public const string ZoneExit = "exit";

        public const string GateRun = "g_run";
        public const string GateCrouch = "g_crouch";
        public const string GateDark = "g_dark";
        public const string GateChat = "g_chat";
        public const string GateItems = "g_items";
        public const string GateDoors = "g_doors";
        public const string GatePuzzle = "g_puzzle";
        public const string GateStage = "g_stage";
        public const string GateFuga = "g_fuga";

        [Header("Tela")]
        [SerializeField] private TutorialHud hud;
        [SerializeField] private ViewerPopulationSO viewers;

        [Header("Lições")]
        [SerializeField] private TutorialButton hiddenButton;
        [SerializeField] private ItemPickable batteryPickable;
        [SerializeField] private GameObject blockedBox;
        [SerializeField] private Door lockedDoor;
        [SerializeField] private Door openDoor;
        [SerializeField] private GameObject[] audienceProps;
        [SerializeField] private PuzzleManagerBase puzzle;
        [SerializeField] private MissionOwnershipSelector puzzleSelector;
        [SerializeField] private DonationManager donationManager;
        [SerializeField] private DonationDefinition tutorialDonation;
        [SerializeField] private ElectricCircuit circuit;
        [SerializeField] private TutorialMonsterRemote monsterRemote;

        [Header("Fuga")]
        [Tooltip("Onde o monstro é solto quando o jogador entra no corredor da fuga.")]
        [SerializeField] private Transform monsterReleasePoint;

        [Tooltip("Para onde o jogador volta se o monstro pegar ele três vezes.")]
        [SerializeField] private Transform fugaRetryPoint;

        [Tooltip("Portas do corredor da fuga. Abrem quando a fuga começa, para o jogador fechar atrás de si.")]
        [SerializeField] private Door[] escapeDoors;

        [Header("Bateria no tutorial")]
        [Tooltip("No prefab a bateria tem máximo int.MaxValue (nunca acaba). No tutorial ela vira 0-100 para a barra mexer.")]
        [SerializeField] private int tutorialBatteryMax = 100;
        [SerializeField] private int tutorialBatteryDrainPerSecond = 1;
        [SerializeField] private float darkRoomBattery = 40f;

        [Header("Ritmo")]
        [SerializeField, Min(0.2f)] private float minLineSeconds = 1.8f;
        [SerializeField, Min(0f)] private float secondsPerCharacter = 0.045f;

        // ------------------------------------------------------------------ estado

        private GameObject _player;
        private PlayerState _state;
        private PlayerInventory _inventory;
        private PlayerInteractor _interactor;
        private AudienceContributor _contributor;
        private PlayerMissionHolder _missionHolder;
        private Health _health;
        private Flashlight _flashlight;
        private HudAnimationPanel _chatPanel;
        private HudAnimationPanel _missionPanel;
        private Rigidbody _body;

        private int _step;
        private string _stepTitle;
        private float _runSeconds;
        private int _batteryItemId = -1;
        private bool _doorRattled;
        private bool _mainMissionReceived;
        private bool _mainMissionCompleted;
        private bool _finished;
        private int _hitsTaken;
        private bool _inFuga;

        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // ------------------------------------------------------------------ ciclo de vida

        private void OnEnable()
        {
            Door.OnDoorBlocked += Door_OnBlocked;
            Health.OnAnyHealthChanged += Health_OnAnyHealthChanged;
        }

        private void OnDisable()
        {
            Door.OnDoorBlocked -= Door_OnBlocked;
            Health.OnAnyHealthChanged -= Health_OnAnyHealthChanged;

            if (_state != null) _state.OnRunEvent -= PlayerState_OnRunEvent;

            if (_missionHolder != null)
            {
                _missionHolder.OnMainMissionReceived -= MissionHolder_OnMainMissionReceived;
                _missionHolder.OnMainMissionCompleted -= MissionHolder_OnMainMissionCompleted;
            }

            TutorialChat.Forget();
        }

        private void Start()
        {
            if (batteryPickable != null) _batteryItemId = batteryPickable.ItemId;

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return WaitForWorld();

            StartCoroutine(HoldAudience());

            yield return LessonMovement();
            yield return LessonCrouchLimit();
            yield return LessonFlashlight();
            yield return LessonChat();
            yield return LessonItems();
            yield return LessonDoors();
            yield return LessonAudience();
            yield return LessonPuzzle();
            yield return LessonRecording();
            yield return LessonDonation();
            yield return LessonLights();
            yield return LessonEscape();

            yield return Finish();
        }

        /// <summary>Espera o host subir, o jogador nascer, o chat dele ligar e o monstro virar estátua.</summary>
        private IEnumerator WaitForWorld()
        {
            while (TutorialNetworkBootstrap.Instance == null || !TutorialNetworkBootstrap.Instance.IsReady) yield return null;

            _player = TutorialNetworkBootstrap.Instance.LocalPlayer;

            while (!TutorialChat.IsReady) yield return null;

            _state = _player.GetComponent<PlayerState>();
            _inventory = _player.GetComponent<PlayerInventory>();
            _interactor = _player.GetComponent<PlayerInteractor>();
            _contributor = _player.GetComponent<AudienceContributor>();
            _missionHolder = _player.GetComponent<PlayerMissionHolder>();
            _health = _player.GetComponent<Health>();
            _body = _player.GetComponent<Rigidbody>();
            _flashlight = _player.GetComponentInChildren<Flashlight>(true);

            ChatManager chat = _player.GetComponentInChildren<ChatManager>(true);
            _chatPanel = chat != null ? chat.GetComponent<HudAnimationPanel>() : null;

            PlayerMissionHolderUi missionUi = _player.GetComponentInChildren<PlayerMissionHolderUi>(true);
            _missionPanel = missionUi != null ? missionUi.GetComponent<HudAnimationPanel>() : null;

            _state.OnRunEvent += PlayerState_OnRunEvent;
            _missionHolder.OnMainMissionReceived += MissionHolder_OnMainMissionReceived;
            _missionHolder.OnMainMissionCompleted += MissionHolder_OnMainMissionCompleted;

            while (monsterRemote != null && !monsterRemote.IsReady) yield return null;

            PrepareBattery();

            if (lockedDoor != null) lockedDoor.CloseAndLock(100000f);

            // Um instante para o chat do jogador acordar e o fade da cena terminar.
            yield return new WaitForSeconds(1.2f);
        }

        // ------------------------------------------------------------------ lições

        private IEnumerator LessonMovement()
        {
            Step(1, "Movimento", $"Ande até a marca amarela no chão com {Key(TutorialKeys.Move)}");

            yield return Say(
                "Oi! Bem-vindo à live. Eu sou a Lia, moderadora do chat. Vou te ensinar tudo por aqui.",
                "Este é o chat. Cada mensagem é de alguém assistindo a sua live. Fique de olho nele!",
                $"Primeiro, andar: use {Key(TutorialKeys.Move)}. Vá até a marca amarela.");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneWalkMark), 14f,
                Hint(Moderator, $"Use {Key(TutorialKeys.Move)} para andar. A marca amarela está ali na frente."),
                Hint(null, "anda pra frente mano kkkk"));

            Flavor(2, "andou!!", "primeiros passos do streamer", "boa boa", "ele anda sim");

            // Zerado antes de abrir: quem sai correndo enquanto a moderadora ainda fala já conta.
            _runSeconds = 0f;

            TutorialGate.Open(GateRun);
            SetObjective($"Segure {Key(TutorialKeys.Run)} e corra até o fim do corredor");

            yield return Say(
                $"Agora correr: segure {Key(TutorialKeys.Run)} enquanto anda. Corra até o fim do corredor.",
                "Repare na barra de stamina: correr gasta, e quando acaba você precisa recuperar.");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneRunEnd) && _runSeconds >= 1.2f, 18f,
                Hint(Moderator, $"Segure {Key(TutorialKeys.Run)} e {Key("W")} ao mesmo tempo para correr."),
                Hint(null, "segura o shift!!!"),
                Hint(Moderator, $"Se chegou andando, tudo bem: corre um pouco segurando {Key(TutorialKeys.Run)} para eu ver."));

            yield return Say(
                "Isso! Correr é rápido, mas faz barulho. Na partida, o monstro ouve.",
                $"Por último: segure {Key(TutorialKeys.Crouch)} para agachar.");

            SetObjective($"Segure {Key(TutorialKeys.Crouch)} para agachar");

            float crouched = 0f;

            yield return WaitFor(() =>
                {
                    crouched = _state.IsCrouching ? crouched + Time.deltaTime : 0f;
                    return crouched >= 0.4f;
                }, 12f,
                Hint(Moderator, $"Segure {Key(TutorialKeys.Crouch)} (não é só apertar uma vez)."),
                Hint(null, "agacha ai pra gnt ver"));

            yield return Say("Agachado você anda mais devagar, mas faz menos barulho e cabe em lugares baixos.");

            Flavor(2, "kkkkk agachadinho", "ninja", "modo furtivo ativado");
            TutorialGate.Open(GateCrouch);
        }

        private IEnumerator LessonCrouchLimit()
        {
            Step(2, "Agachar", $"Agache ({Key(TutorialKeys.Crouch)}) e passe por baixo do cano");

            yield return Say(
                "Agachar serve para passar por baixo das coisas. Viu o cano atravessando a sala?",
                $"Em pé não passa. Segure {Key(TutorialKeys.Crouch)} e passe por baixo dele.");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneCrouchBeam), 15f,
                Hint(Moderator, $"Chegue no cano segurando {Key(TutorialKeys.Crouch)} o tempo todo."),
                Hint(null, "agacha antes de chegar no cano"));

            SetObjective($"Agora passe por baixo da mesa (segure {Key(TutorialKeys.Crouch)})");

            yield return Say(
                "Boa! Agora a mesa. Ela é ainda mais baixa: só passa agachado, e o seu chapéu amassa um pouquinho.",
                "Fique agachado até sair do outro lado, senão você não levanta.");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneCrouchTable), 15f,
                Hint(Moderator, $"Segure {Key(TutorialKeys.Crouch)} e entre por baixo da mesa."),
                Hint(null, "o chapeu amassa kkkkkkk"));

            yield return Say(
                "Esse é o limite: o vão de baixo da mesa tem 1 metro. Mais baixo que isso, nem agachado.",
                "Aquele duto na parede é o exemplo: baixo demais. Se não cabe com o chapéu amassado, não cabe.");

            Flavor(2, "o chapeu achatando kkkkkk", "mano passou raspando", "gostei do chapéu");
            TutorialGate.Open(GateDark);
        }

        private IEnumerator LessonFlashlight()
        {
            Step(3, "Lanterna", "Entre na sala escura");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneDarkRoom), 15f,
                Hint(Moderator, "A próxima sala está aberta. Pode entrar, não morde (ainda)."));

            SetBattery(darkRoomBattery);
            SetObjective($"Aperte {Key(TutorialKeys.Flashlight)} para ligar a lanterna");

            yield return Say(
                "Escuro, né? A lanterna é sua melhor amiga.",
                $"Aperte {Key(TutorialKeys.Flashlight)} para ligar e desligar.");

            yield return WaitFor(IsFlashlightOn, 10f,
                Hint(Moderator, $"A tecla da lanterna é {Key(TutorialKeys.Flashlight)}."),
                Hint(null, "LIGA A LANTERNA"));

            Flavor(2, "agora sim", "que medo", "LUZ");

            yield return Say(
                "Viu a barra da bateria? A lanterna gasta enquanto está ligada. Desligue quando não precisar.",
                "E o clique de ligar faz um barulhinho que o monstro pode ouvir.",
                "Agora ache a saída desta sala no escuro.");

            TutorialGate.Open(GateChat);
            SetObjective("Ache a saída da sala escura");

            yield return WaitFor(() =>
                {
                    // Sem pilha aqui o jogador ficaria preso no escuro: a lição é achar a porta, não economizar.
                    if (GetBattery() < 8f) SetBattery(20f);
                    return TutorialZone.WasEntered(ZoneDarkExit);
                }, 25f,
                Hint(null, "a saida ta na parede do fundo"),
                Hint(Moderator, "Procure a luz verde no fundo da sala. É a porta."));
        }

        private IEnumerator LessonChat()
        {
            Step(4, "Chat", "Leia o chat");

            yield return Say(
                "Agora sobre mim e a galera: o chat.",
                "Aqui você recebe as mensagens da live. Os viewers reagem ao que você filma e ao que acontece.",
                "E eles ajudam! Quando você empaca, o chat manda dica: missão, porta trancada, luz apagada...");

            if (_chatPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Chat)} para esconder o chat");
                yield return Say($"Se o chat atrapalhar, aperte {Key(TutorialKeys.Chat)} para esconder. Tenta!");

                yield return WaitFor(() => !_chatPanel.IsVisible, 10f,
                    Hint(Moderator, $"A tecla é {Key(TutorialKeys.Chat)}."));

                SetObjective($"Aperte {Key(TutorialKeys.Chat)} de novo para trazer o chat de volta");

                // A moderadora fala com o chat fechado: a linha fica esperando no painel.
                yield return WaitFor(() => _chatPanel.IsVisible, 8f,
                    Hint(Moderator, $"Aperte {Key(TutorialKeys.Chat)} de novo! Sem o chat você perde as dicas."));

                yield return Say("Bem-vindo de volta! Nunca fique muito tempo sem olhar o chat.");
            }

            if (hiddenButton != null) hiddenButton.IsArmed = true;

            SetObjective("Ache o botão escondido que abre a próxima porta (o chat ajuda)");

            yield return Say(
                "Teste: tem um botão escondido nesta sala. Ele abre a próxima porta.",
                "Não vou dizer onde. Se demorar, o chat vai começar a dar dica. Leia!");

            yield return WaitFor(() => hiddenButton == null || hiddenButton.IsPressed, 10f, 9f,
                Hint(null, "procura atrás das coisas"),
                Hint(null, "olha atrás do armário!!"),
                Hint(null, "ATRÁS DO ARMÁRIO GRANDE MANO"),
                Hint(Moderator, $"O pessoal tem razão: o botão está atrás do armário grande. Mire nele e use {Key(TutorialKeys.Interact)}."));

            Flavor(3, "EU FALEI", "o chat carregando", "de nada kkkkk", "chat > streamer");

            yield return Say("Viu? Quando você empacar na partida, olhe o chat: alguém sempre sabe.");

            TutorialGate.Open(GateItems);
        }

        private IEnumerator LessonItems()
        {
            Step(5, "Itens e bateria", $"Mire na pilha em cima da mesa e use {Key(TutorialKeys.Interact)} para pegar");

            yield return Say(
                "Itens! Quando você mira em algo que dá para usar, aparece o contorno e a tecla.",
                $"Mire na pilha em cima da mesa e use {Key(TutorialKeys.Interact)} para pegar.");

            yield return WaitFor(HasBatteryInInventory, 14f,
                Hint(Moderator, $"Chegue perto da mesa, mire na pilha (ela fica contornada) e use {Key(TutorialKeys.Interact)}."),
                Hint(null, "pega a pilha"));

            SetObjective($"Aperte {Key(TutorialKeys.Slot1)} para segurar a pilha");

            yield return Say(
                "Ela foi para o inventário, aqui embaixo. São 4 espaços, nas teclas 1 a 4.",
                $"Aperte {Key(BatterySlotKey())} para segurar a pilha.");

            yield return WaitFor(IsHoldingBattery, 10f,
                Hint(Moderator, $"Aperte o número do espaço onde a pilha está ({Key(BatterySlotKey())})."));

            SetObjective($"Use {Key(TutorialKeys.Use)} para colocar a pilha na lanterna");

            yield return Say($"Com a pilha na mão, use {Key(TutorialKeys.Use)} para recarregar a lanterna. Olhe a barra!");

            // A pilha sai do inventário quando é usada. Comparar com a bateria de "antes" travava quem
            // usava a pilha enquanto a moderadora ainda falava: o "antes" já era a bateria cheia.
            yield return WaitFor(() => !HasBatteryInInventory(), 10f,
                Hint(Moderator, $"Segurando a pilha, é só {Key(TutorialKeys.Use)}."));

            Flavor(2, "bateria cheia", "agora vai", "economiza essa ein");

            yield return Say(
                "Bateria cheia! Pilhas ficam espalhadas pelo teatro. Pegue sempre que achar.",
                "Agora: nem tudo dá para pegar. Mire naquela caixa do lado.");

            SetObjective("Mire na caixa ao lado da mesa");

            float hovered = 0f;

            yield return WaitFor(() =>
                {
                    hovered = IsHoveringBlockedBox() ? hovered + Time.deltaTime : 0f;
                    return hovered >= 0.4f;
                }, 12f,
                Hint(Moderator, "A caixa de papelão em cima do caixote. Mire nela."));

            yield return Say(
                "Contorno vermelho e INDISPONÍVEL: essa caixa é da missão de outro jogador. Só o dono pega.",
                "Com o inventário cheio, pegar um item troca pelo que está na sua mão. Fique de olho no que carrega.");

            Flavor(2, "nao é seu kkkkk", "ladrão de caixa", "devolve");
            TutorialGate.Open(GateDoors);
        }

        private IEnumerator LessonDoors()
        {
            _doorRattled = false;

            Step(6, "Portas", $"Tente abrir a porta da esquerda ({Key(TutorialKeys.Interact)})");

            yield return Say(
                "Portas! Elas abrem e fecham com a tecla de interagir.",
                $"Tente abrir a porta da esquerda: mire nela e use {Key(TutorialKeys.Interact)}.");

            yield return WaitFor(() => _doorRattled, 15f,
                Hint(Moderator, $"A porta da esquerda. Mire e {Key(TutorialKeys.Interact)}."));

            yield return Say(
                "Chacoalhou e não abriu! Porta assim está sendo segurada pelo monstro (ou ele sabotou).",
                "Na partida, isso passa sozinho em alguns segundos. Ou você acha outro caminho.");

            SetObjective($"Abra a porta da direita ({Key(TutorialKeys.Interact)})");
            yield return Say("Use a porta da direita.");

            yield return WaitFor(() => openDoor == null || !openDoor.IsClosed, 12f,
                Hint(Moderator, $"Porta da direita, {Key(TutorialKeys.Interact)}."));

            SetObjective($"Passe e feche a porta atrás de você ({Key(TutorialKeys.Interact)})");

            yield return Say(
                "Ela abre para o lado oposto de quem abre. Agora passe e feche atrás de você.",
                "Cuidado: porta batendo derruba quem estiver no caminho. Não faça isso com seus amigos (ou faça).");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneDoorsAfter) && TutorialZone.IsInside(ZoneDoorsAfter) && (openDoor == null || openDoor.IsClosed), 14f,
                Hint(Moderator, $"Do outro lado, vire para a porta e use {Key(TutorialKeys.Interact)} para fechar."),
                Hint(null, "fecha a porta!!"));

            yield return Say(
                "Porta fechada segura o monstro um pouco: ele tem que forçar para passar.",
                "Mas abrir e fechar faz barulho. Use quando estiver fugindo.");

            Flavor(2, "fecha a porta pra ele n entrar", "tranca tudo", "porta salva vidas");
        }

        /// <summary>
        /// Até a lição de audiência, a live tem uma plateia estável: sem ela a audiência cai sozinha
        /// (é a regra do jogo para quem não mostra nada) e o chat de fundo emudece justo nas lições
        /// em que o jogador ainda não sabe gravar. A partir da lição de audiência, vale a regra real.
        /// </summary>
        private IEnumerator HoldAudience()
        {
            AudienceManager audience = null;
            float waited = 0f;

            while (audience == null && waited < 10f)
            {
                audience = AudienceManager.Instance;
                waited += Time.deltaTime;
                yield return null;
            }

            if (audience == null) yield break;

            if (audience.CurrentAudience < 140f) audience.SubmitGain(140f - audience.CurrentAudience);

            while (_holdAudience)
            {
                audience.SubmitPresence();
                yield return new WaitForSeconds(1f);
            }
        }

        private bool _holdAudience = true;

        private IEnumerator LessonAudience()
        {
            _holdAudience = false;

            Step(7, "Audiência", "Grave um objeto com o olho: deixe ele na câmera até registrar");

            yield return Say(
                "Agora o mais importante da live: AUDIÊNCIA.",
                "Coisas que o público quer ver têm um olho em cima. Para gravar, é só manter na câmera.",
                "Olhe para um desses objetos por uns segundos e veja o que acontece com o olho.");

            yield return WaitFor(() => RegisteredProps() >= 1, 18f,
                Hint(Moderator, "Chegue perto (uns 5 metros) e mantenha o objeto no meio da tela por 3 segundos."),
                Hint(null, "fica olhando pra coroa"));

            yield return Say(
                "Viu o olho ficar VERMELHO? Isso quer dizer que a audiência daquele objeto entrou.",
                "Enquanto grava, o olho respira. Vermelho é gravado e pago. Olhe a barra de audiência subir!");

            SetObjective("Grave mais um objeto diferente");

            yield return WaitFor(() => RegisteredProps() >= 2, 18f,
                Hint(Moderator, "Outro objeto com olho: a espada ou a máscara."));

            yield return Say(
                "Cada objeto paga uma vez. Depois de um tempo o olho volta ao normal e ele paga de novo.",
                "Se você ficar muito tempo sem mostrar nada, a audiência cai. Mostre coisas sempre!",
                "Na partida, 75% de audiência é uma das chaves para abrir a saída.");

            Flavor(3, "CONTEÚDO", "mostra a coroa de novo", "que live", "subiu a audiencia");
            TutorialGate.Open(GatePuzzle);
        }

        private IEnumerator LessonPuzzle()
        {
            Step(8, "Missão e puzzle", "Entre na sala da missão");

            yield return WaitFor(() => TutorialZone.WasEntered(ZonePuzzleRoom), 15f,
                Hint(Moderator, "A próxima sala abriu. Vamos para a sua primeira missão."));

            StartPuzzle();

            yield return Say(
                "Chegou missão! Ela aparece no painel de missões, no canto da tela.",
                "Cada missão tem um TÍTULO (o que é) e uma DICA (como resolver). Leia com calma.");

            if (_missionPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Missions)} para esconder e mostrar o painel de missões");

                yield return WaitFor(() => !_missionPanel.IsVisible, 10f,
                    Hint(Moderator, $"A tecla do painel de missões é {Key(TutorialKeys.Missions)}."));

                yield return WaitFor(() => _missionPanel.IsVisible, 8f,
                    Hint(Moderator, $"{Key(TutorialKeys.Missions)} de novo para ver a missão."));
            }

            string title = puzzleSelector != null && puzzleSelector.Mission != null ? puzzleSelector.Mission.missionName : "a missão";
            string clue = puzzleSelector != null && puzzleSelector.Mission != null ? puzzleSelector.Mission.instructions : string.Empty;

            yield return Say(
                $"A missão é \"{title}\". A dica: \"{clue}\"",
                "Interprete: as caixas têm símbolos. Os suportes perto das paredes também. Combine os iguais!",
                $"Pegue uma caixa ({Key(TutorialKeys.Interact)}), segure ({Key("1-4")}), mire no suporte e use {Key(TutorialKeys.Use)}.");

            UpdatePuzzleObjective();

            yield return WaitFor(() =>
                {
                    UpdatePuzzleObjective();
                    return puzzle == null || puzzle.IsComplete;
                }, 30f, 30f,
                Hint(null, "estrela com estrela, circulo com circulo"),
                Hint(Moderator, "Olhe o desenho na caixa e procure o mesmo desenho no suporte. Aparece uma caixa fantasma verde quando está certo de colocar."),
                Hint(null, "segura a caixa (numero) e clica no suporte"),
                Hint(Moderator, $"Se pôs no lugar errado, pode pegar de volta com {Key(TutorialKeys.Interact)} e tentar de novo."));

            Flavor(3, "GENIO", "resolveu rapidinho", "puzzle facil kkkk", "big brain");

            yield return Say(
                "Missão completa! Ela some do painel e o chat comemora.",
                "Na partida cada um recebe missões diferentes. Ajudem uns aos outros!");
        }

        private IEnumerator LessonRecording()
        {
            Step(9, "Missão de gravação", "Leia a missão principal no painel");

            // A missão principal nasce do MissionManager quando as missões pessoais acabam.
            float waited = 0f;
            while (!_mainMissionReceived && waited < 4f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (!_mainMissionReceived)
            {
                Debug.LogWarning("Tutorial: a missão principal não chegou pelo MissionManager; o tutorial a entrega direto.");
                RevealMainMissionFallback();
            }

            yield return Say(
                "Terminando as missões, aparece a MISSÃO PRINCIPAL: a gravação que a live inteira quer ver.",
                "Grave o monstro no palco principal. Sim, o monstro. Ele está na próxima sala.",
                "Calma: só aqui no tutorial ele está congelado.");

            TutorialGate.Open(GateStage);
            SetObjective("Pare na marca do chão e grave o monstro no palco (monstro no meio da tela)");

            yield return Say("A dica fala em marcas no chão: tem uma fita amarela em frente ao palco. Grave dali!");

            yield return WaitFor(() => _mainMissionCompleted, 25f,
                Hint(Moderator, "Fique na fita amarela em frente ao palco e deixe o monstro no meio da tela."),
                Hint(null, "filma ele no palco!!!"),
                Hint(Moderator, "Chegue mais perto: o palco e o monstro precisam aparecer juntos na câmera, a menos de 10 metros."));

            StopMainMissionRecorder();

            Flavor(4, "QUE CENA", "CLIPA ISSO", "to com medo", "AAAAAA", "melhor live");

            yield return Say(
                "GRAVADO! Na partida, missão principal completa + 75% de audiência abrem a porta da saída.",
                "Aí cada um pega o seu disquete e corre para fora.");
        }

        private IEnumerator LessonDonation()
        {
            Step(10, "Doação", "Leia o pedido da doação");

            bool spawned = SpawnTutorialDonation();

            yield return Say(
                "Opa, DOAÇÃO! O cartão no meio da tela é um pedido de quem doou.",
                "Cumprir a doação dá audiência e deixa o chat feliz. Elas têm tempo para acabar.");

            if (!spawned)
            {
                yield return Say("(Essa doação não chegou por algum problema. Seguimos sem ela.)");
                yield break;
            }

            SetObjective($"Cumpra a doação: grave o monstro por 5 segundos ({Key(TutorialKeys.Donate)} mostra os pedidos)");

            yield return Say(
                $"Depois do aviso, o pedido fica num ícone embaixo das missões. Aperte {Key(TutorialKeys.Donate)} para ver de novo.",
                "Este pede para gravar o monstro por 5 segundos. A barrinha do cartão enche enquanto você grava.",
                "Tem doação que pede para GRITAR perto do monstro. Aí é com o seu microfone. Coragem!");

            yield return WaitFor(IsDonationComplete, 20f,
                Hint(Moderator, "Mantenha o monstro na câmera até a barrinha da doação encher."),
                Hint(null, "grava ele de novo"));

            Flavor(4, "valeu pela doação", "DOOU", "pix do terror", "obrigado pelo conteúdo");

            yield return Say("Doação completa! Valeu demais.");
        }

        private IEnumerator LessonLights()
        {
            Step(11, "Luzes", "...");

            yield return Say("Ih... o que foi isso?");

            SetLightsOut(true);

            SetObjective("A luz caiu! Ache o quadro de luz e use-o para religar");

            yield return Say(
                "O monstro pode apagar as luzes do teatro inteiro! Ligue a lanterna.",
                "Para religar, ache o QUADRO DE LUZ (uma caixa cinza na parede) e use.");

            yield return WaitFor(() => !IsLightsOut(), 18f,
                Hint(null, "o quadro tá na parede do lado do palco"),
                Hint(Moderator, $"Ligue a lanterna ({Key(TutorialKeys.Flashlight)}) e procure na parede lateral do palco."),
                Hint(Moderator, $"A caixa cinza com uma alavanca. Mire nela e use {Key(TutorialKeys.Interact)}."));

            Flavor(3, "LUZ VOLTOU", "ufa", "nem tava com medo", "eletricista");

            yield return Say("Luz de volta! Na partida o quadro pode estar em outro lugar. O chat costuma lembrar onde.");
        }

        private IEnumerator LessonEscape()
        {
            Step(12, "Fugir do monstro", "Prepare-se...");

            yield return Say(
                "Última lição: FUGIR. Na partida o monstro anda solto pelo teatro.",
                "Ele te acha pela visão e pelo barulho. Correr, falar alto, porta e lanterna fazem barulho.",
                "Se ele te vir: corra, feche portas atrás de você e saia da linha de visão dele.",
                "Vou descongelar ele. Quando a porta abrir, corra até a SAÍDA!");

            yield return new WaitForSeconds(0.8f);

            OpenEscapeDoors();
            TutorialGate.Open(GateFuga);
            SetObjective($"Corra até a SAÍDA! ({Key(TutorialKeys.Run)} corre · feche as portas atrás de você)");

            yield return WaitFor(() => TutorialZone.WasEntered(ZoneFugaStart), 12f,
                Hint(Moderator, "Pela porta aberta no fundo do palco. Vai!"));

            ReleaseMonster();
            _inFuga = true;
            _hitsTaken = 0;

            Flavor(4, "CORREEEE", "ELE TA VINDO", "AAAAAAAAA", "fecha a porta fecha a porta");

            float nudge = 0f;

            yield return WaitFor(() =>
                {
                    nudge += Time.deltaTime;

                    // O monstro perde o rastro em curva fechada: um barulho no jogador mantém a perseguição.
                    if (nudge >= 2.5f)
                    {
                        nudge = 0f;
                        KeepMonsterOnTrail();
                    }

                    return TutorialZone.WasEntered(ZoneExit);
                }, 20f, 15f,
                Hint(null, "segue o corredor e vira a direita"),
                Hint(Moderator, "Siga as luzes verdes até a placa de SAÍDA."));

            _inFuga = false;

            if (monsterRemote != null) monsterRemote.SetPaused(true);

            Flavor(4, "ESCAPOU", "GG", "que fuga", "sobreviveu!!!", "clipa clipa");
        }

        private IEnumerator Finish()
        {
            _finished = true;

            yield return Say(
                "VOCÊ SOBREVIVEU! Tutorial completo.",
                $"Lembrete: {Key(TutorialKeys.Pause)} pausa e tem as opções (volume, mouse, microfone).",
                "Se você morrer na partida, vira espectador e assiste os amigos. Então não morra.",
                "Boa live e boa sorte!");

            SetObjective("Tutorial concluído!");

            if (_state != null) _state.SetInputLocked(Interfaces.InputLockReason.EndGame, true);

            if (hud != null)
            {
                hud.ShowEndCard(
                    "Tutorial concluído!",
                    "Na partida: faça suas missões, grave coisas para a audiência, cumpra as doações, " +
                    "complete a missão principal e fuja pela saída antes que o monstro pegue vocês.",
                    "Voltar ao menu", ReturnToMenu,
                    "Jogar de novo", Restart);
            }
        }

        // ------------------------------------------------------------------ ações

        private void PrepareBattery()
        {
            if (_flashlight == null) return;

            SetField(_flashlight, "batteryPercentMax", tutorialBatteryMax);
            SetField(_flashlight, "batteryPercentDecreasePerSecond", tutorialBatteryDrainPerSecond);
            SetBattery(tutorialBatteryMax);
        }

        private void SetBattery(float value)
        {
            if (_flashlight == null) return;

            if (GetField(_flashlight, "_currentBattery") is NetworkVariable<float> battery)
            {
                battery.Value = Mathf.Clamp(value, 0f, tutorialBatteryMax);
            }

            SetField(_flashlight, "_batteryDead", value <= 0f);
        }

        private float GetBattery()
        {
            return _flashlight != null && GetField(_flashlight, "_currentBattery") is NetworkVariable<float> battery
                ? battery.Value
                : 0f;
        }

        private bool IsFlashlightOn()
        {
            return _flashlight != null && GetField(_flashlight, "_isFlashlightOn") is NetworkVariable<bool> on && on.Value;
        }

        private bool HasBatteryInInventory() => BatterySlot() >= 0;

        private int BatterySlot()
        {
            if (_inventory == null || _batteryItemId < 0) return -1;

            for (int i = 0; i < _inventory.MaxInventorySize; i++)
            {
                if (_inventory.GetItemInSlot(i) == _batteryItemId) return i;
            }

            return -1;
        }

        private string BatterySlotKey()
        {
            int slot = BatterySlot();
            return slot >= 0 ? (slot + 1).ToString() : TutorialKeys.Slot1;
        }

        private bool IsHoldingBattery()
        {
            int slot = BatterySlot();
            return slot >= 0 && _inventory.CurrentSelectedSlot == slot + 1;
        }

        private bool IsHoveringBlockedBox()
        {
            if (_interactor == null || blockedBox == null) return false;
            if (!_interactor.IsHoveredBlocked) return false;

            return _interactor.HoveredInteractable is Component hovered && hovered != null && hovered.transform.IsChildOf(blockedBox.transform);
        }

        private int RegisteredProps()
        {
            if (_contributor == null || audienceProps == null) return 0;

            int count = 0;

            foreach (GameObject prop in audienceProps)
            {
                if (prop != null && _contributor.HasBeenSeen(prop)) count++;
            }

            return count;
        }

        private void StartPuzzle()
        {
            if (puzzle == null || puzzleSelector == null) return;

            ulong local = NetworkManager.Singleton.LocalClientId;

            puzzleSelector.AssignOwner(local);
            puzzle.RequestSpawn();

            if (puzzleSelector.Mission != null) _missionHolder.ReceivePersonalMission(puzzleSelector.Mission);
        }

        private void UpdatePuzzleObjective()
        {
            if (puzzle == null) return;

            int total = 0;
            int correct = 0;

            foreach (ItemSlotTotem totem in FindObjectsByType<ItemSlotTotem>(FindObjectsSortMode.None))
            {
                total++;
                if (totem.IsCorrect) correct++;
            }

            SetObjective($"Coloque cada caixa no suporte com o mesmo símbolo  ({correct}/{Mathf.Max(total, 1)})");
        }

        private void RevealMainMissionFallback()
        {
            MissionManager manager = FindFirstObjectByType<MissionManager>();
            if (manager == null || manager.CurrentContract == null || manager.CurrentContract.mainMission == null) return;

            GameObject prefab = manager.CurrentContract.mainMissionPrefab;

            if (prefab != null)
            {
                GameObject mission = Instantiate(prefab);
                if (mission.TryGetComponent(out NetworkObject networkObject)) networkObject.Spawn();
                if (mission.TryGetComponent(out MainMission main)) main.StartMission();
            }

            _missionHolder.ReceiveMainMission(manager.CurrentContract.mainMission);
        }

        /// <summary>
        /// O MissionsRecorder confere a cada meio segundo para sempre, mesmo depois de completo, e
        /// manda o RPC de novo a cada vez. No tutorial, terminou, sai de cena.
        /// </summary>
        private static void StopMainMissionRecorder()
        {
            foreach (MissionsRecorder recorder in FindObjectsByType<MissionsRecorder>(FindObjectsSortMode.None))
            {
                if (recorder.NetworkObject != null && recorder.NetworkObject.IsSpawned) recorder.NetworkObject.Despawn();
            }
        }

        private bool SpawnTutorialDonation()
        {
            if (donationManager == null || tutorialDonation == null || _state == null) return false;

            MethodInfo spawn = typeof(DonationManager).GetMethod("SpawnDonation", Private);

            if (spawn == null)
            {
                Debug.LogWarning("Tutorial: DonationManager.SpawnDonation não foi encontrado por reflexão.");
                return false;
            }

            spawn.Invoke(donationManager, new object[] { tutorialDonation, _state });
            return true;
        }

        private bool IsDonationComplete()
        {
            if (donationManager == null || tutorialDonation == null) return true;

            foreach (DonationNetworkState state in donationManager.NetworkStates)
            {
                if (state.DonationId.ToString() != tutorialDonation.donationId) continue;

                if (state.State == DonationState.Completed) return true;

                if (state.State == DonationState.Expired)
                {
                    // Não deveria acontecer (a doação do tutorial não expira), mas se acontecer manda outra.
                    SpawnTutorialDonation();
                    return false;
                }
            }

            return false;
        }

        private MonsterSabotage Sabotage => monsterRemote != null && monsterRemote.Monster != null ? monsterRemote.Monster.MonsterSabotage : null;

        private void SetLightsOut(bool lightsOut)
        {
            MonsterSabotage sabotage = Sabotage;
            if (sabotage == null) return;

            if (GetField(sabotage, "_lightsOut") is NetworkVariable<bool> flag) flag.Value = lightsOut;
        }

        private bool IsLightsOut()
        {
            MonsterSabotage sabotage = Sabotage;
            return sabotage != null && sabotage.LightsOut;
        }

        /// <summary>
        /// Abre as portas da fuga para longe de quem vem do palco. É o mesmo caminho que o monstro usa
        /// ao forçar (a única abertura pública da porta): segura a porta uns segundos, e por isso
        /// acontece bem antes de o jogador chegar nela.
        /// </summary>
        private void OpenEscapeDoors()
        {
            if (escapeDoors == null) return;

            Vector3 from = monsterReleasePoint != null ? monsterReleasePoint.position : transform.position;

            foreach (Door door in escapeDoors)
            {
                if (door != null && door.IsClosed) door.ForceOpenFrom(from);
            }
        }

        private void ReleaseMonster()
        {
            if (monsterRemote == null || _player == null) return;

            Vector3 from = monsterReleasePoint != null ? monsterReleasePoint.position : monsterRemote.Monster.transform.position;
            monsterRemote.Hunt(from, _player.transform.position);
        }

        private void KeepMonsterOnTrail()
        {
            if (monsterRemote == null || monsterRemote.Monster == null || _player == null) return;
            if (monsterRemote.IsPaused || monsterRemote.Monster.IsHunting) return;

            Components.Perception.NoiseBus.Report(_player.transform.position, 40f, Components.Perception.NoiseType.Footstep, _player.transform);
        }

        private void ReturnToMenu()
        {
            if (hud != null) hud.HideEndCard();

            PauseMenuUi pause = _player != null ? _player.GetComponentInChildren<PauseMenuUi>(true) : null;

            if (pause != null)
            {
                pause.ReturnToMainMenu();
                return;
            }

            SceneManager.LoadScene("MainMenu");
        }

        private async void Restart()
        {
            if (hud != null) hud.HideEndCard();

            string scene = gameObject.scene.name;

            HostSingleton.instance?.gameManager?.Dispose();
            await NetworkSession.EnsureStoppedAsync();

            SceneManager.LoadScene(scene);
        }

        // ------------------------------------------------------------------ eventos

        private void PlayerState_OnRunEvent(bool isRunning)
        {
            if (isRunning) _runSeconds += Time.deltaTime;
        }

        private void MissionHolder_OnMainMissionReceived(MissionSO mission) => _mainMissionReceived = true;

        private void MissionHolder_OnMainMissionCompleted(MissionSO mission) => _mainMissionCompleted = true;

        private void Door_OnBlocked(Vector3 position)
        {
            if (lockedDoor == null) return;

            if (Vector3.Distance(position, lockedDoor.transform.position) < 2f) _doorRattled = true;
        }

        /// <summary>
        /// No tutorial ninguém morre: depois de cada golpe o jogador volta a ficar inteiro. Na terceira
        /// vez que o monstro alcança, a fuga recomeça do início do corredor.
        /// </summary>
        private void Health_OnAnyHealthChanged(Health health, float previous, float current)
        {
            if (_finished || health == null || health != _health) return;
            if (current >= previous) return;

            _hitsTaken++;
            StartCoroutine(HealAfterHit());
        }

        private IEnumerator HealAfterHit()
        {
            yield return new WaitForSeconds(1.2f);

            if (_health != null) _health.RestoreHealth(_health.MaxHealth);

            if (!_inFuga)
            {
                yield return Say("Ufa, aqui no tutorial você não morre. Na partida, cuidado!");
                yield break;
            }

            if (_hitsTaken < 3)
            {
                Post(Moderator, "Ele te pegou! No tutorial eu te curo. Na partida, cada golpe tira vida. CORRE!");
                yield break;
            }

            _hitsTaken = 0;

            Post(Moderator, "Vamos de novo! Corra assim que a porta abrir e feche as portas atrás de você.");

            if (monsterRemote != null) monsterRemote.SendHome();

            yield return new WaitForSeconds(1.5f);

            if (TutorialNetworkBootstrap.Instance != null) TutorialNetworkBootstrap.Instance.Teleport(fugaRetryPoint);

            yield return new WaitForSeconds(2.5f);

            ReleaseMonster();
        }

        // ------------------------------------------------------------------ chat e objetivos

        private readonly struct ChatHint
        {
            public readonly string Viewer;
            public readonly string Text;

            public ChatHint(string viewer, string text)
            {
                Viewer = viewer;
                Text = text;
            }
        }

        /// <summary>Dica dita por <paramref name="viewer"/>; null sorteia um viewer da plateia.</summary>
        private static ChatHint Hint(string viewer, string text) => new ChatHint(viewer, text);

        private void Step(int step, string title, string objective)
        {
            _step = step;
            _stepTitle = title;

            if (hud != null) hud.SetObjective(step, TotalSteps, title, objective);
        }

        private void SetObjective(string objective)
        {
            if (hud != null) hud.SetObjective(_step, TotalSteps, _stepTitle, objective);
        }

        private IEnumerator Say(params string[] lines)
        {
            foreach (string line in lines)
            {
                Post(Moderator, line);
                yield return new WaitForSeconds(Mathf.Max(minLineSeconds, line.Length * secondsPerCharacter));
            }
        }

        private void Post(string viewer, string text)
        {
            if (string.IsNullOrWhiteSpace(viewer)) viewer = PickViewer();

            string name = viewer == Moderator ? $"<color=#FFD54A>[MOD]</color> {viewer}" : viewer;
            TutorialChat.Post(name, text);
        }

        private string PickViewer()
        {
            if (viewers != null && viewers.TryPickName(out string name)) return name;

            return "viewer";
        }

        private static void Flavor(int count, params string[] lines) => TutorialChat.Flavor(lines, count, 0.45f);

        /// <summary>
        /// Espera <paramref name="done"/> ficar verdadeiro. Sem progresso por <paramref name="firstHint"/>
        /// segundos, solta a próxima dica; depois uma a cada <paramref name="repeat"/> segundos, a última
        /// se repetindo.
        /// </summary>
        private IEnumerator WaitFor(Func<bool> done, float firstHint, float repeat, params ChatHint[] hints)
        {
            float timer = 0f;
            int next = 0;

            while (!done())
            {
                timer += Time.deltaTime;

                float threshold = next == 0 ? firstHint : repeat;

                if (hints != null && hints.Length > 0 && timer >= threshold)
                {
                    timer = 0f;
                    ChatHint hint = hints[Mathf.Min(next, hints.Length - 1)];
                    Post(hint.Viewer, hint.Text);
                    next++;
                }

                yield return null;
            }
        }

        private IEnumerator WaitFor(Func<bool> done, float firstHint, params ChatHint[] hints) =>
            WaitFor(done, firstHint, Mathf.Max(8f, firstHint), hints);

        private static string Key(string key) => $"<b><color=#FFD54A>[{key}]</color></b>";

        // ------------------------------------------------------------------ reflexão

        private static object GetField(object target, string name)
        {
            FieldInfo field = FindField(target.GetType(), name);
            return field?.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = FindField(target.GetType(), name);

            if (field == null)
            {
                Debug.LogWarning($"Tutorial: campo {name} não existe em {target.GetType().Name}.");
                return;
            }

            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null) return field;
            }

            return null;
        }
    }
}
