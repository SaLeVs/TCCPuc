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
    /// <para>Quem ensina é a turma do chat: S4lev, camisolinha, aaaquerodormir e tutujooj (mesmos
    /// nomes da plateia do jogo). Os quatro se apresentam no começo; depois cada assunto é puxado por
    /// um deles, sorteado, que fala até o fim do assunto (dicas incluídas). Uma frase por mensagem,
    /// curta o bastante para o chat não encolher a fonte.</para>
    ///
    /// <para>As lições conferem a ação a todo quadro: o que o jogador já fez (até antes de pedirem)
    /// conta na hora, e a fala que ensina uma ação já feita é descartada. As falas de uma sala só
    /// começam quando o jogador entra nela, e a porta da próxima (<see cref="TutorialDoorGate"/>: luz
    /// vermelha que vira verde) só abre depois que a turma terminou de falar. Nada aqui imita um
    /// sistema: são as peças do jogo ligadas por fora, e nenhum script do jogo foi alterado.</para>
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        private const int TotalSteps = 12;

        // A turma que conduz o tutorial (nomes como aparecem no chat).
        private const string Dev = "S4lev";
        private const string Camis = "camisolinha";
        private const string Sono = "aaaquerodormir";
        private const string Tutu = "tutujooj";

        // Zonas (TutorialZone) e portões (TutorialDoorGate) que o gerador da cena cria com estes nomes.
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
        public const string ZoneRunStart = "run_start";
        public const string ZoneCrouchRoom = "crouch_room";
        public const string ZoneItemsRoom = "items_room";
        public const string ZoneDoorsRoom = "doors_room";

        // Placas e rótulos de parede de cada lição: somem junto com as marcas de chão quando ela acaba.
        public const string SignMovement = "sign_1";
        public const string SignCrouch = "sign_2";
        public const string SignFlashlight = "sign_3";
        public const string SignChat = "sign_4";
        public const string SignItems = "sign_5";
        public const string SignDoors = "sign_6";
        public const string SignAudience = "sign_7";
        public const string SignPuzzle = "sign_8";
        public const string SignRecording = "sign_9";
        public const string SignLights = "sign_11";

        // Marcas de chão (TutorialFloorHint) que somem quando a etapa delas termina.
        public const string FloorHintBattery = "battery";
        public const string FloorHintRecordMark = "record_mark";

        // Altura (pixels de referência) do cartão de doação do jogo no topo da tela.
        private const float DonationCardHeight = 190f;

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

        [Tooltip("Portas de enfeite (a saída de emergência do fim): ficam trancadas o tutorial inteiro.")]
        [SerializeField] private Door[] decorDoors;

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

        [Header("Ritmo do chat")]
        [SerializeField, Min(0.2f)] private float minLineSeconds = 4.5f;
        [SerializeField, Min(0f)] private float secondsPerCharacter = 0.1f;

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
        private DonationUIController _donationUi;
        private float _bannerDrop;

        private bool _ready;
        private int _step;
        private string _stepTitle;
        private int _batteryItemId = -1;
        private bool _holdAudience = true;
        private bool _finished;
        private int _hitsTaken;
        private bool _inFuga;

        // O que o jogador já fez, conferido a todo quadro desde o começo: ação feita antes de pedirem
        // também vale.
        private float _runSeconds;
        private float _crouchSeconds;
        private bool _crouched;
        private bool _flashlightUsed;
        private bool _chatHidden;
        private bool _puzzleStarted;
        private bool _missionPanelHidden;
        private bool _batteryPicked;
        private bool _batteryHeld;
        private bool _batteryUsed;
        private float _blockedHoverSeconds;
        private bool _blockedHovered;
        private bool _doorRattled;
        private bool _openDoorOpened;
        private bool _mainMissionReceived;
        private bool _mainMissionCompleted;
        private float _nextGateComment;

        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // ------------------------------------------------------------------ fila de falas

        private readonly struct Line
        {
            public readonly string Who;
            public readonly string Text;
            public readonly Func<bool> Skip;

            public Line(string who, string text, Func<bool> skip)
            {
                Who = who;
                Text = text;
                Skip = skip;
            }
        }

        private static readonly string[] Teachers = { Dev, Camis, Sono, Tutu };

        /// <summary>Quem puxou o assunto atual (fala até o fim dele).</summary>
        private string _topic;

        private float _lastPostTime = -100f;

        private readonly List<Line> _speech = new();
        private ChatDirector _chatDirector;
        private float _nextLineTime;

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

            // Armado desde o começo: quem achar o botão antes da hora já cumpriu a lição.
            if (hiddenButton != null) hiddenButton.IsArmed = true;

            StartCoroutine(Run());
        }

        private void Update()
        {
            MuteAudienceChat();

            if (!_ready) return;

            Track();
            Speak();
            FollowDonationCard();
        }

        private IEnumerator Run()
        {
            yield return WaitForWorld();

            StartCoroutine(HoldAudience());

            yield return Intro();
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

            Finish();
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
            _flashlight = _player.GetComponentInChildren<Flashlight>(true);

            ChatManager chat = _player.GetComponentInChildren<ChatManager>(true);
            _chatPanel = chat != null ? chat.GetComponent<HudAnimationPanel>() : null;

            // Sem conversa de fundo da plateia: o chat do tutorial é só de quem ensina.
            _chatDirector = chat != null ? chat.GetComponent<ChatDirector>() : null;
            if (_chatDirector != null) SetField(_chatDirector, "ambientEnabled", false);

            PlayerMissionHolderUi missionUi = _player.GetComponentInChildren<PlayerMissionHolderUi>(true);
            _missionPanel = missionUi != null ? missionUi.GetComponent<HudAnimationPanel>() : null;
            _donationUi = _player.GetComponentInChildren<DonationUIController>(true);

            _state.OnRunEvent += PlayerState_OnRunEvent;
            _missionHolder.OnMainMissionReceived += MissionHolder_OnMainMissionReceived;
            _missionHolder.OnMainMissionCompleted += MissionHolder_OnMainMissionCompleted;

            while (monsterRemote != null && !monsterRemote.IsReady) yield return null;

            PrepareBattery();

            TutorialDoorGate.LockAll();
            if (lockedDoor != null) lockedDoor.CloseAndLock(1000000f);

            if (decorDoors != null)
            {
                foreach (Door door in decorDoors)
                {
                    if (door != null) door.CloseAndLock(1000000f);
                }
            }

            // Uns segundos para o jogador olhar o cenário antes da primeira mensagem.
            _nextLineTime = Time.time + 3f;
            _ready = true;
        }

        /// <summary>
        /// Confere a todo quadro o que o jogador fez. As lições só leem estas marcas (ou o estado do
        /// jogo direto), então nada depende de o jogador agir depois da fala certa.
        /// </summary>
        private void Track()
        {
            if (_state != null)
            {
                _crouchSeconds = _state.IsCrouching ? _crouchSeconds + Time.deltaTime : 0f;
                if (_crouchSeconds >= 0.35f) _crouched = true;
            }

            if (IsFlashlightOn()) _flashlightUsed = true;
            if (_chatPanel != null && !_chatPanel.IsVisible) _chatHidden = true;
            if (_puzzleStarted && _missionPanel != null && !_missionPanel.IsVisible) _missionPanelHidden = true;

            bool hasBattery = HasBatteryInInventory();
            if (hasBattery) _batteryPicked = true;
            if (IsHoldingBattery()) _batteryHeld = true;
            if (_batteryPicked && !hasBattery) _batteryUsed = true;

            _blockedHoverSeconds = IsHoveringBlockedBox() ? _blockedHoverSeconds + Time.deltaTime : 0f;
            if (_blockedHoverSeconds >= 0.3f) _blockedHovered = true;

            if (openDoor != null && !openDoor.IsClosed) _openDoorOpened = true;
        }

        // ------------------------------------------------------------------ lições

        /// <summary>Os quatro se apresentam e pedem atenção ao chat. Só aqui falam todos de uma vez.</summary>
        private IEnumerator Intro()
        {
            Step(1, "Movimento", "Leia o chat, à direita da tela");

            SayAs(Dev, "Oi! Aqui é S4lev.");
            SayAs(Camis, "oii, aqui é camisolinha!");
            SayAs(Sono, "aqui é aaaquerodormir, oi");
            SayAs(Tutu, "e aqui é tutujooj, salve!");

            NewTopic();
            Say("Vamos te ensinar a jogar.");
            Say("Presta atenção aqui no chat!");
            Say("As dicas chegam por aqui.");
            Say("Em cima da tela tem a tarefa.");

            yield return WaitSpeech();
        }

        private IEnumerator LessonMovement()
        {
            Func<bool> walked = () => TutorialZone.WasEntered(ZoneWalkMark);
            Func<bool> ran = () => TutorialZone.WasEntered(ZoneRunEnd) && _runSeconds >= 1.2f;

            Step(1, "Movimento", $"Ande até a marca amarela no chão com {Key(TutorialKeys.Move)}");

            NewTopic();
            Teach(walked, $"Ande com {Key(TutorialKeys.Move)}.");
            Teach(walked, "Vá até a marca amarela.");

            yield return WaitFor(walked, 14f,
                "A marca está no meio da sala.",
                "É só andar pra frente.");

            TutorialFloorHint.Hide(ZoneWalkMark);

            Say("Boa, andou certinho!");
            Say("Viu a luz em cima da porta?");
            Say("Vermelha: ainda não passa.");
            Say("Verde: a porta abre sozinha.");

            yield return OpenNext(GateRun);
            yield return EnterRoom(ZoneRunStart);

            SetObjective($"Segure {Key(TutorialKeys.Run)} e corra até o fim do corredor");

            NewTopic();
            Teach(ran, $"Segure {Key(TutorialKeys.Run)} pra correr.");
            Teach(ran, "Corra até o fim do corredor.");

            yield return WaitFor(ran, 18f,
                $"Segure {Key(TutorialKeys.Run)} e {Key("W")} juntos.",
                "Corra até a faixa amarela.",
                "Andou? Agora corre um pouco.");

            TutorialFloorHint.Hide(ZoneRunEnd);

            Say("Correr é rápido.");
            Say("Mas faz barulho.");
            Say("Na partida, o monstro ouve!");

            TutorialFloorHint.Hide(SignMovement);
            yield return OpenNext(GateCrouch);
        }

        private IEnumerator LessonCrouchLimit()
        {
            Func<bool> crouched = () => _crouched;
            Func<bool> underDuct = () => TutorialZone.WasEntered(ZoneCrouchBeam) || TutorialZone.WasEntered(ZoneCrouchTable);
            Func<bool> underTable = () => TutorialZone.WasEntered(ZoneCrouchTable);

            yield return EnterRoom(ZoneCrouchRoom);

            Step(2, "Agachar", $"Segure {Key(TutorialKeys.Crouch)} para agachar");

            NewTopic();
            Teach(crouched, $"Segure {Key(TutorialKeys.Crouch)} pra agachar.");

            yield return WaitFor(crouched, 12f,
                "Segure, não é só apertar.");

            Say("Agachado você anda devagar.");
            Say("Mas faz menos barulho.");

            SetObjective($"Agache ({Key(TutorialKeys.Crouch)}) e passe por baixo do duto de ar");
            Teach(underDuct, "Em pé não dá pra passar.");
            Teach(underDuct, "Passe por baixo do duto.");

            yield return WaitFor(underDuct, 15f,
                $"Segure {Key(TutorialKeys.Crouch)} até passar.",
                "Agache antes de chegar nele.");

            SetObjective($"Agora passe por baixo da mesa (segure {Key(TutorialKeys.Crouch)})");
            Teach(underTable, "Agora a mesa, mais baixa.");
            Teach(underTable, "Só passa agachado.");

            yield return WaitFor(underTable, 15f,
                "Entre agachado por baixo dela.");

            Say("Seu chapéu até amassou, kkk");
            Say("Esse é o limite: 1 metro.");
            Say("A grade de 0,8 m não dá.");

            TutorialFloorHint.Hide(SignCrouch);
            yield return OpenNext(GateDark);
        }

        private IEnumerator LessonFlashlight()
        {
            Func<bool> lit = () => _flashlightUsed;
            Func<bool> out_ = () => TutorialZone.WasEntered(ZoneDarkExit);

            yield return EnterRoom(ZoneDarkRoom);

            Step(3, "Lanterna", $"Aperte {Key(TutorialKeys.Flashlight)} para ligar a lanterna");
            SetBattery(darkRoomBattery);

            NewTopic();
            Say("Que escuro, hein?");
            Teach(lit, $"Aperte {Key(TutorialKeys.Flashlight)} pra lanterna.");

            yield return WaitFor(lit, 10f,
                $"A tecla é {Key(TutorialKeys.Flashlight)}.",
                "Liga a lanterna!");

            Say("Viu a barra da bateria?");
            Say("A lanterna gasta ligada.");
            Say("Desligue quando não precisar.");

            yield return OpenNext(GateChat);

            SetObjective("Ache a saída da sala escura");
            Teach(out_, "Agora ache a saída.");
            Teach(out_, "Procure a luz verde.");

            yield return WaitFor(() =>
                {
                    // Sem pilha aqui o jogador ficaria preso no escuro: a lição é achar a porta, não economizar.
                    if (GetBattery() < 8f) SetBattery(20f);
                    return out_();
                }, 25f,
                "A saída é na parede do fundo.",
                "Fica à direita, no fundo.");

            TutorialFloorHint.Hide(SignFlashlight);
        }

        private IEnumerator LessonChat()
        {
            Func<bool> hidden = () => _chatHidden;
            Func<bool> shown = () => _chatPanel == null || _chatPanel.IsVisible;
            Func<bool> pressed = () => hiddenButton == null || hiddenButton.IsPressed;

            // A zona da entrada desta sala é a saída da sala escura (a lição anterior já esperou por ela).
            Step(4, "Chat", "Leia o chat");

            NewTopic();
            Say("Aqui é o chat da live.");
            Say("A galera reage ao que rola.");

            if (_chatPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Chat)} para esconder o chat");
                Teach(hidden, $"Aperte {Key(TutorialKeys.Chat)} pra esconder.");

                yield return WaitFor(hidden, 10f, $"A tecla é {Key(TutorialKeys.Chat)}.");

                SetObjective($"Aperte {Key(TutorialKeys.Chat)} de novo para trazer o chat de volta");

                // Com o chat fechado a linha fica esperando no painel.
                Teach(shown, $"Aperte {Key(TutorialKeys.Chat)} de novo pra voltar.");

                yield return WaitFor(shown, 8f, "Sem chat você perde dicas.");

                Say("Isso, o chat voltou!");
            }

            SetObjective("Ache o botão escondido que abre a próxima porta (o chat ajuda)");

            NewTopic();
            Teach(pressed, "Tem um botão escondido aqui.");
            Teach(pressed, "Ele abre a próxima porta.");

            yield return WaitFor(pressed, 10f, 9f,
                "Procure atrás das coisas.",
                "Olhe atrás do armário!",
                "No canto, atrás do armário.",
                $"No botão: {Key(TutorialKeys.Interact)}.");

            Say("Viu? Se empacar, olhe o chat.");
            Say("Alguém sempre sabe.");

            TutorialFloorHint.Hide(SignChat);
            yield return OpenNext(GateItems);
        }

        private IEnumerator LessonItems()
        {
            Func<bool> picked = () => _batteryPicked;
            Func<bool> held = () => _batteryHeld || _batteryUsed;
            Func<bool> used = () => _batteryUsed;
            Func<bool> hovered = () => _blockedHovered;

            yield return EnterRoom(ZoneItemsRoom);

            Step(5, "Itens e bateria", $"Mire na pilha em cima da mesa e use {Key(TutorialKeys.Interact)} para pegar");

            NewTopic();
            Teach(picked, "Tem uma pilha na mesa.");
            Teach(picked, $"Na pilha: {Key(TutorialKeys.Interact)}.");

            yield return WaitFor(picked, 14f,
                "Chegue perto e mire na pilha.",
                "Ela fica com contorno.");

            TutorialFloorHint.Hide(FloorHintBattery);

            SetObjective($"Aperte {Key(BatterySlotKey())} para segurar a pilha");
            Teach(held, "Ela foi pro inventário.");
            Teach(held, "São 4 espaços, teclas 1 a 4.");
            Teach(held, $"Aperte {Key(BatterySlotKey())} pra segurar.");

            yield return WaitFor(held, 10f, "Aperte o número do espaço.");

            SetObjective($"Use {Key(TutorialKeys.Use)} para colocar a pilha na lanterna");
            Teach(used, $"Use com {Key(TutorialKeys.Use)}.");
            Teach(used, "Isso recarrega a lanterna.");

            // A pilha sai do inventário quando é usada.
            yield return WaitFor(used, 10f, "Segure a pilha e use.");

            Say("A bateria encheu!");

            SetObjective("Mire na caixa em cima do case de metal");

            NewTopic();
            Teach(hovered, "Nem tudo dá pra pegar.");
            Teach(hovered, "Mire na caixa em cima do case.");

            yield return WaitFor(hovered, 12f,
                "A caixa ao lado da mesa.",
                "Só mire nela, sem pegar.");

            Say("Viu? Contorno vermelho.");
            Say("E escrito INDISPONÍVEL.");
            Say("É missão de outro jogador.");
            Say("Só o dono pode pegar.");

            TutorialFloorHint.Hide(SignItems);
            yield return OpenNext(GateDoors);
        }

        private IEnumerator LessonDoors()
        {
            Func<bool> passed = () => TutorialZone.WasEntered(ZoneDoorsAfter);
            Func<bool> rattled = () => _doorRattled || passed();
            Func<bool> opened = () => _openDoorOpened || passed();
            Func<bool> closedBehind = () => passed() && TutorialZone.IsInside(ZoneDoorsAfter) && (openDoor == null || openDoor.IsClosed);

            yield return EnterRoom(ZoneDoorsRoom);

            Step(6, "Portas", $"Tente abrir a porta da esquerda ({Key(TutorialKeys.Interact)})");

            NewTopic();
            Teach(rattled, $"Abra com {Key(TutorialKeys.Interact)}.");
            Teach(rattled, "Tente a porta da esquerda.");

            yield return WaitFor(rattled, 15f, "A da esquerda. Mire e use.");

            Say("Ela chacoalha e não abre.");
            Say("O monstro está segurando.");

            SetObjective($"Abra a porta da direita ({Key(TutorialKeys.Interact)})");
            Teach(opened, "Tente a porta da direita.");

            yield return WaitFor(opened, 12f, $"Direita: {Key(TutorialKeys.Interact)}.");

            SetObjective($"Passe e feche a porta atrás de você ({Key(TutorialKeys.Interact)})");
            Teach(closedBehind, "Agora passe por ela.");
            Teach(closedBehind, "E feche atrás de você.");

            yield return WaitFor(closedBehind, 14f,
                "Vire pra porta e use de novo.",
                "Fecha a porta!");

            Say("Porta fechada ajuda.");
            Say("O monstro tem que forçar.");

            yield return WaitSpeech();
            TutorialFloorHint.Hide(SignDoors);
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

        private IEnumerator LessonAudience()
        {
            Func<bool> one = () => RegisteredProps() >= 1;
            Func<bool> two = () => RegisteredProps() >= 2;

            // A sala da audiência é a de trás da porta da direita: a lição de portas já terminou nela.
            _holdAudience = false;

            Step(7, "Audiência", "Grave um objeto com o olho: deixe ele na câmera até registrar");

            NewTopic();
            Teach(one, "Viu os OLHOS nos objetos?");
            Teach(one, "É o que a plateia quer ver.");
            Teach(one, "Olhe pra um por 3 segundos.");

            yield return WaitFor(one, 18f,
                "Chegue a uns 5 metros dele.",
                "Deixe no meio da tela.");

            Say("O olho ficou VERMELHO!");
            Say("A audiência dele entrou.");

            SetObjective("Grave mais um objeto diferente");
            Teach(two, "Grave mais um, outro objeto.");

            yield return WaitFor(two, 18f, "A espada ou a máscara.");

            Say("A saída pede 75% de audiência.");

            TutorialFloorHint.Hide(SignAudience);
            yield return OpenNext(GatePuzzle);
        }

        private IEnumerator LessonPuzzle()
        {
            Func<bool> solved = () => puzzle == null || puzzle.IsComplete;
            Func<bool> panelHidden = () => _missionPanelHidden || solved();
            Func<bool> panelShown = () => _missionPanel == null || _missionPanel.IsVisible || solved();

            yield return EnterRoom(ZonePuzzleRoom);

            Step(8, "Missão e puzzle", "Leia a missão no painel");

            StartPuzzle();

            NewTopic();
            Say("Chegou uma missão!");
            Say("Ela fica no painel, no canto.");
            Say("O TÍTULO diz o que é.");
            Say("A DICA diz como resolver.");

            if (_missionPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Missions)} para esconder e mostrar o painel de missões");
                Teach(panelHidden, $"O painel esconde com {Key(TutorialKeys.Missions)}.");

                yield return WaitFor(panelHidden, 10f, $"A tecla é {Key(TutorialKeys.Missions)}.");

                Teach(panelShown, $"Aperte {Key(TutorialKeys.Missions)} de novo.");

                yield return WaitFor(panelShown, 8f, "Sem ele você não vê a missão.");
            }

            string title = puzzleSelector != null && puzzleSelector.Mission != null ? puzzleSelector.Mission.missionName : "Exaustor";

            UpdatePuzzleObjective();

            Teach(solved, $"A missão é \"{title}\".");
            Teach(solved, "A dica fala em canos tortos.");
            Teach(solved, "É a parede da direita.");
            Teach(solved, $"No cano: {Key(TutorialKeys.Interact)}.");
            Teach(solved, "Cada vez, ele gira um pouco.");

            yield return WaitFor(() =>
                {
                    UpdatePuzzleObjective();
                    return solved();
                }, 25f, 25f,
                "Comece numa ponta do cano.",
                "Siga pedaço por pedaço.",
                "A faixa conta os canos certos.",
                "Veja os canos soltos na mesa.");

            Say("Missão completa!");
            Say("Ela some do painel.");

            TutorialFloorHint.Hide(SignPuzzle);
        }

        private IEnumerator LessonRecording()
        {
            Func<bool> recorded = () => _mainMissionCompleted;

            Step(9, "Missão de gravação", "Leia a missão principal no painel");

            // A missão principal nasce do MissionManager quando as missões pessoais acabam. Em geral
            // chega no mesmo quadro; o prazo é só para o caso de algo no jogo mudar.
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

            NewTopic();
            Teach(recorded, "Chegou a MISSÃO PRINCIPAL!");
            Teach(recorded, "Grave o monstro no palco.");
            Teach(recorded, "O palco é na próxima sala.");

            yield return OpenNext(GateStage);
            yield return EnterRoom(ZoneStageRoom);

            SetObjective("Pare na marca do chão e grave o monstro no palco (monstro no meio da tela)");

            Say("Calma, ele está congelado.");
            Say("Só aqui no tutorial, kkk");
            Teach(recorded, "A dica fala em marca no chão.");
            Teach(recorded, "Tem um X amarelo ali.");
            Teach(recorded, "Grave o monstro dali!");

            yield return WaitFor(recorded, 25f,
                "Monstro no meio da tela.",
                "Fique a menos de 10 metros.",
                "Palco e monstro na tela.");

            StopMainMissionRecorder();
            TutorialFloorHint.Hide(FloorHintRecordMark);

            Say("GRAVADO!");
            Say("Missão principal completa.");
            Say("Ela + audiência abrem a saída.");

            yield return WaitSpeech();
            TutorialFloorHint.Hide(SignRecording);
        }

        private IEnumerator LessonDonation()
        {
            Step(10, "Doação", "Leia o pedido da doação");

            bool spawned = SpawnTutorialDonation();

            NewTopic();
            Say("OPA, uma DOAÇÃO!");

            if (!spawned)
            {
                Say("A doação não chegou, seguimos.");
                yield return WaitSpeech();
                yield break;
            }

            Say("O cartão mostra o pedido.");

            SetObjective($"Cumpra a doação: grave o monstro por 5 segundos ({Key(TutorialKeys.Donate)} mostra os pedidos)");

            Teach(IsDonationComplete, "Ele pede pra gravar o monstro.");
            Teach(IsDonationComplete, "Grave ele por 5 segundos.");
            Teach(IsDonationComplete, "A barrinha do cartão enche.");

            yield return WaitFor(IsDonationComplete, 20f,
                "Deixe o monstro na câmera.",
                "Até a barrinha encher.");

            Say("Doação completa!");
            Say($"Depois, {Key(TutorialKeys.Donate)} mostra os pedidos.");
            Say("Tem doação que pede GRITO.");
            Say("Aí é no seu microfone, kkk");

            yield return WaitSpeech();
        }

        private IEnumerator LessonLights()
        {
            Func<bool> fixedLights = () => !IsLightsOut();

            Step(11, "Luzes", "A luz caiu! Ache o quadro de luz e use-o para religar");

            SetLightsOut(true);

            NewTopic();
            Say("Opa, a luz caiu!");
            Teach(fixedLights, "O monstro apaga as luzes.");
            Teach(fixedLights, $"Ligue a lanterna, {Key(TutorialKeys.Flashlight)}.");
            Teach(fixedLights, "Ache o QUADRO DE LUZ.");
            Teach(fixedLights, "É uma caixa cinza na parede.");

            yield return WaitFor(fixedLights, 18f,
                "Fica na parede da esquerda.",
                $"No quadro: {Key(TutorialKeys.Interact)}.");

            Say("Luz de volta!");
            Say("Na partida ele muda de lugar.");

            yield return WaitSpeech();
            TutorialFloorHint.Hide(SignLights);
        }

        private IEnumerator LessonEscape()
        {
            Func<bool> started = () => TutorialZone.WasEntered(ZoneFugaStart);
            Func<bool> escaped = () => TutorialZone.WasEntered(ZoneExit);

            Step(12, "Fugir do monstro", "Leia o chat");

            NewTopic();
            Say("Última lição: FUGIR!");
            Say("Na partida ele anda solto.");
            Say("Ele te acha pela visão.");
            Say("E pelo barulho que você faz.");
            Say("A porta do fundo vai abrir.");
            Say("Passou dela, ele acorda.");
            Say("Aí corra até a SAÍDA!");

            yield return WaitSpeech();

            OpenEscapeDoors();
            TutorialDoorGate.Open(GateFuga);

            SetObjective($"Corra pela porta do fundo do palco até a SAÍDA! ({Key(TutorialKeys.Run)} corre)");

            yield return WaitFor(started, 12f, "Pela porta da luz verde, vai!");

            ReleaseMonster();
            _inFuga = true;
            _hitsTaken = 0;

            SetObjective($"Corra até a SAÍDA! ({Key(TutorialKeys.Run)} corre · feche as portas atrás de você)");

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

                    return escaped();
                }, 20f, 15f,
                "Siga o corredor.",
                "Siga as placas de SAÍDA.");

            _inFuga = false;

            if (monsterRemote != null) monsterRemote.SetPaused(true);
        }

        private void Finish()
        {
            _finished = true;
            TutorialFloorHint.Hide(ZoneExit);

            SetObjective("Tutorial concluído!");

            string host = NewTopic();
            Say("VOCÊ SOBREVIVEU!");
            Say("Tutorial completo.");
            Say($"{Key(TutorialKeys.Pause)} pausa e abre as opções.");

            // Despedida: os outros três também falam.
            string[] goodbyes = { "boa live!", "boa sorte!", "tchau tchau!" };
            int next = 0;

            foreach (string teacher in Teachers)
            {
                if (teacher != host && next < goodbyes.Length) SayAs(teacher, goodbyes[next++]);
            }

            if (_state != null) _state.SetInputLocked(Interfaces.InputLockReason.EndGame, true);

            if (hud != null)
            {
                hud.ShowEndCard(
                    "Tutorial concluído!",
                    "Na partida: faça suas missões, grave coisas para a audiência, cumpra as doações, " +
                    "complete a missão principal e fuja pela saída antes que o monstro te pegue.",
                    "Voltar ao menu", ReturnToMenu,
                    "Jogar de novo", Restart);
            }
        }

        /// <summary>
        /// Fecha a sala: espera a turma terminar de falar (o jogador não sai da sala sem ver tudo o
        /// que era dela) e só então abre a porta da próxima.
        /// </summary>
        private IEnumerator OpenNext(string gate)
        {
            yield return WaitSpeech();
            TutorialDoorGate.Open(gate);
        }

        /// <summary>
        /// Espera o jogador entrar na sala da próxima lição. Nada da sala nova é dito antes: as falas
        /// dela começam quando ele chega.
        /// </summary>
        private IEnumerator EnterRoom(string zone)
        {
            if (TutorialZone.WasEntered(zone)) yield break;

            SetObjective("A porta abriu! Vá para a próxima sala");

            while (!TutorialZone.WasEntered(zone)) yield return null;
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

            _puzzleStarted = true;

            // O prefab do cano vem com o rótulo padrão do marcador ("Pegar"). Só nas peças desta
            // cena, já instanciadas: o prefab do jogo fica como está.
            FieldInfo label = typeof(InteractionMarker).GetField("actionLabel", Private);
            if (label == null) return;

            foreach (PipeTotem pipe in FindObjectsByType<PipeTotem>(FindObjectsSortMode.None))
            {
                foreach (InteractionMarker marker in pipe.GetComponentsInChildren<InteractionMarker>(true)) label.SetValue(marker, "Girar");
            }
        }

        private void UpdatePuzzleObjective()
        {
            if (puzzle == null) return;

            int total = 0;
            int correct = 0;

            foreach (PipeTotem pipe in FindObjectsByType<PipeTotem>(FindObjectsSortMode.None))
            {
                if (!pipe.IsSpawned) continue;

                total++;
                if (pipe.IsCorrect) correct++;
            }

            SetObjective($"Gire os canos da parede ({Key(TutorialKeys.Interact)}) até o caminho ficar inteiro  ({correct}/{Mathf.Max(total, 1)} no lugar)");
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
            if (lockedDoor != null && Vector3.Distance(position, lockedDoor.transform.position) < 2f)
            {
                _doorRattled = true;
                return;
            }

            // Porta de lição ainda fechada: alguém do chat explica (sem repetir a cada tentativa).
            if (_finished || !TutorialDoorGate.IsClosedGateAt(position) || Time.time < _nextGateComment) return;
            if (_player != null && Vector3.Distance(_player.transform.position, position) > 4f) return;

            _nextGateComment = Time.time + 8f;
            SayNow("Essa porta ainda não abre.", "Abre quando a luz ficar verde.");
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
                SayNow("Ufa! Aqui você não morre.", "Na partida, cuidado!");
                yield break;
            }

            if (_hitsTaken < 3)
            {
                SayNow("Ele te pegou! CORRE!", "Na partida, o golpe tira vida.");
                yield break;
            }

            _hitsTaken = 0;

            SayNow("Vamos de novo!", "Corra e feche as portas.");

            if (monsterRemote != null) monsterRemote.SendHome();

            yield return new WaitForSeconds(1.5f);

            if (TutorialNetworkBootstrap.Instance != null) TutorialNetworkBootstrap.Instance.Teleport(fugaRetryPoint);

            yield return new WaitForSeconds(2.5f);

            ReleaseMonster();
        }

        // ------------------------------------------------------------------ chat e objetivos

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

        /// <summary>
        /// Começa um assunto novo com alguém sorteado da turma (nunca quem puxou o assunto anterior).
        /// Quem começa termina: todas as falas e dicas do assunto saem com o mesmo nome.
        /// </summary>
        private string NewTopic()
        {
            string who;

            do who = Teachers[UnityEngine.Random.Range(0, Teachers.Length)];
            while (who == _topic);

            _topic = who;
            return who;
        }

        private string Speaker => _topic ?? Dev;

        /// <summary>Fala do assunto atual na fila: sai no ritmo de leitura, ninguém espera por ela.</summary>
        private void Say(string text) => Enqueue(Speaker, text, null);

        /// <summary>Fala com nome fixo (apresentação e despedida), fora do assunto atual.</summary>
        private void SayAs(string who, string text) => Enqueue(who, text, null);

        /// <summary>
        /// Fala que ensina uma ação: se o jogador já fez (<paramref name="done"/>) quando chegar a vez
        /// dela, ela é descartada.
        /// </summary>
        private void Teach(Func<bool> done, string text) => Enqueue(Speaker, text, done);

        /// <summary>
        /// Aviso que não pode esperar a fila (golpe do monstro, porta de lição): entra na frente das
        /// falas pendentes, na ordem dada.
        /// </summary>
        private void SayNow(params string[] lines)
        {
            for (int i = 0; i < lines.Length; i++) _speech.Insert(i, new Line(Speaker, lines[i], null));

            // Urgente, mas não colado na mensagem anterior: dá pelo menos 2 s para ler.
            _nextLineTime = Mathf.Min(_nextLineTime, Mathf.Max(Time.time, _lastPostTime + 2f));
        }

        private void Enqueue(string who, string text, Func<bool> skip)
        {
            // Uma frase por mensagem, sem quebrar: a fala tem que caber no chat na fonte cheia.
            if (VisibleLength(text) > ChatLineBudget - who.Length - 2)
            {
                Debug.LogWarning($"Tutorial: fala longa para o chat ({VisibleLength(text)} letras com {who}): {text}");
            }

            _speech.Add(new Line(who, text, skip));
        }

        private void Speak()
        {
            if (Time.time < _nextLineTime) return;

            while (_speech.Count > 0)
            {
                Line line = _speech[0];
                _speech.RemoveAt(0);

                // Ensina algo que o jogador já fez: descartada.
                if (line.Skip != null && line.Skip()) continue;

                Post(line.Who, line.Text);
                _lastPostTime = Time.time;

                // Quem está começando lê devagar: tempo mínimo por mensagem, mais por letra.
                _nextLineTime = Time.time + Mathf.Max(minLineSeconds, VisibleLength(line.Text) * secondsPerCharacter);
                return;
            }
        }

        /// <summary>Espera a fila esvaziar e a última mensagem ficar o tempo de leitura na tela.</summary>
        private IEnumerator WaitSpeech()
        {
            while (_speech.Count > 0 || Time.time < _nextLineTime) yield return null;
        }

        private static void Post(string viewer, string text) => TutorialChat.Post(viewer, text);

        // O ChatUi encolhe a fonte quando o texto não cabe em duas linhas: com o nome junto, até uns
        // 45 caracteres a mensagem sai no tamanho cheio.
        private const int ChatLineBudget = 46;

        private static int VisibleLength(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty).Length;

        /// <summary>
        /// O cartão de doação aparece no topo, no centro, bem onde fica a faixa de objetivo: enquanto
        /// ele está na tela (chegou agora ou o jogador abriu com a tecla de doação), a faixa desce para
        /// baixo dele; sem cartão, ela volta para cima.
        /// </summary>
        private void FollowDonationCard()
        {
            if (hud == null) return;

            bool cardShown = _donationUi != null
                             && _donationUi.isActiveAndEnabled
                             && GetField(_donationUi, "_isCardShown") is true;

            float target = cardShown ? DonationCardHeight : 0f;
            if (Mathf.Approximately(_bannerDrop, target)) return;

            _bannerDrop = Mathf.MoveTowards(_bannerDrop, target, 1200f * Time.unscaledDeltaTime);
            hud.SetBannerDrop(_bannerDrop);
        }

        /// <summary>
        /// No tutorial o chat é só da turma que ensina: a conversa da plateia (falas soltas e reações
        /// automáticas do jogo) é descartada antes de sair. As falas do tutorial não passam pela
        /// ChatDirector, então não são afetadas.
        /// </summary>
        private void MuteAudienceChat()
        {
            if (_chatDirector == null) return;

            if (GetField(_chatDirector, "_queue") is System.Collections.IList queue && queue.Count > 0) queue.Clear();

            // Uma reação pode entrar e sair da fila dela no mesmo quadro, antes da limpeza acima:
            // com o intervalo dela sempre cheio, nada sai.
            SetField(_chatDirector, "_gapTimer", 30f);
        }

        /// <summary>
        /// Espera <paramref name="done"/> ficar verdadeiro, conferindo a todo quadro. Sem progresso
        /// por <paramref name="firstHint"/> segundos (contados depois que as falas pendentes saíram),
        /// quem puxou o assunto manda a primeira dica; depois uma a cada <paramref name="repeat"/>
        /// segundos. Cada dica sai uma vez só. A dica que chegar depois de a ação estar feita é
        /// descartada.
        /// </summary>
        private IEnumerator WaitFor(Func<bool> done, float firstHint, float repeat, params string[] hints)
        {
            float timer = 0f;
            int next = 0;

            while (!done())
            {
                if (_speech.Count == 0 && Time.time >= _nextLineTime) timer += Time.deltaTime;

                float threshold = next == 0 ? firstHint : repeat;

                if (hints != null && next < hints.Length && timer >= threshold)
                {
                    timer = 0f;
                    Teach(done, hints[next]);
                    next++;
                }

                yield return null;
            }
        }

        private IEnumerator WaitFor(Func<bool> done, float firstHint, params string[] hints) =>
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
