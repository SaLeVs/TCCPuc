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
    /// <para>Quem ensina é a turma do chat: S4lev, camisolinha, aaaquerodormir e tutujooj (mesmos nomes da plateia do jogo). As falas
    /// entram numa fila e saem no ritmo de leitura, mas <b>ninguém espera por elas</b>: cada lição
    /// confere a ação a todo quadro, e o que o jogador já fez (até antes de pedirem) conta na hora. A
    /// fala que ensina uma ação já feita é descartada. A faixa de baixo sempre mostra o que fazer
    /// agora.</para>
    ///
    /// <para>Cada lição termina abrindo a porta da próxima sala (<see cref="TutorialDoorGate"/>): a luz
    /// de emergência em cima passa de vermelha a verde e a porta abre sozinha. Nada aqui imita um
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
            public readonly int Step;

            /// <summary>Partes de uma mesma fala (quebrada para caber no chat) têm o mesmo grupo.</summary>
            public readonly int Group;

            /// <summary>Primeira parte da fala: só ela decide se a fala sai. Começou, vai até o fim.</summary>
            public readonly bool First;

            public Line(string who, string text, Func<bool> skip, int step, int group, bool first)
            {
                Who = who;
                Text = text;
                Skip = skip;
                Step = step;
                Group = group;
                First = first;
            }
        }

        private int _lineGroup;
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

        private IEnumerator LessonMovement()
        {
            Func<bool> walked = () => TutorialZone.WasEntered(ZoneWalkMark);
            Func<bool> ran = () => TutorialZone.WasEntered(ZoneRunEnd) && _runSeconds >= 1.2f;
            Func<bool> crouched = () => _crouched;

            Step(1, "Movimento", $"Ande até a marca amarela no chão com {Key(TutorialKeys.Move)}");

            Say(Dev, "Oi! Aqui é S4lev, do time do jogo.");
            Say(Dev, "A gente vai te ensinar tudo pelo chat.");
            Teach(walked, Dev, $"Primeiro, andar: {Key(TutorialKeys.Move)}. Vá até a marca amarela no chão.");
            Say(Tutu, "e olha a luz em cima da porta: vermelha = ainda não pode passar. termina a lição que ela fica verde e abre sozinha");

            yield return WaitFor(walked, 14f,
                Hint(Dev, $"Use {Key(TutorialKeys.Move)} para andar. A marca amarela está no meio da sala."),
                Hint(Tutu, "anda pra frente mano kkkk"));


            TutorialDoorGate.Open(GateRun);
            SetObjective($"Segure {Key(TutorialKeys.Run)} e corra até o fim do corredor");

            Teach(ran, Sono, $"agora corre: segura {Key(TutorialKeys.Run)} enquanto anda, até o fim do corredor");

            yield return WaitFor(ran, 18f,
                Hint(Dev, $"Segure {Key(TutorialKeys.Run)} e {Key("W")} ao mesmo tempo para correr."),
                Hint(Sono, "segura o shift!!!"),
                Hint(Sono, $"chegou andando? corre um pouquinho segurando {Key(TutorialKeys.Run)} pra gente ver"));

            Say(Dev, "Isso! Correr é rápido, mas faz barulho. Na partida, o monstro ouve.");

            SetObjective($"Segure {Key(TutorialKeys.Crouch)} para agachar");
            Teach(crouched, Camis, $"por último: segura {Key(TutorialKeys.Crouch)} pra agachar");

            yield return WaitFor(crouched, 12f,
                Hint(Dev, $"Segure {Key(TutorialKeys.Crouch)} (não é só apertar uma vez)."),
                Hint(Tutu, "agacha ai pra gnt ver"));

            Say(Dev, "Agachado você anda mais devagar, mas faz menos barulho e cabe em lugares baixos.");

            TutorialDoorGate.Open(GateCrouch);
        }

        private IEnumerator LessonCrouchLimit()
        {
            Func<bool> underDuct = () => TutorialZone.WasEntered(ZoneCrouchBeam) || TutorialZone.WasEntered(ZoneCrouchTable);
            Func<bool> underTable = () => TutorialZone.WasEntered(ZoneCrouchTable);

            Step(2, "Agachar", $"Agache ({Key(TutorialKeys.Crouch)}) e passe por baixo do duto de ar");

            Teach(underDuct, Dev, $"Em pé não passa. Segure {Key(TutorialKeys.Crouch)} e passe por baixo dele.");

            yield return WaitFor(underDuct, 15f,
                Hint(Dev, $"Chegue no duto segurando {Key(TutorialKeys.Crouch)} o tempo todo."),
                Hint(Sono, "agacha antes de chegar no duto"));

            SetObjective($"Agora passe por baixo da mesa (segure {Key(TutorialKeys.Crouch)})");

            Teach(underTable, Camis, "agora a mesa: é mais baixa ainda, só passa agachado e o seu chapéu amassa um pouquinho kkk");

            yield return WaitFor(underTable, 15f,
                Hint(Dev, $"Segure {Key(TutorialKeys.Crouch)} e entre por baixo da mesa."),
                Hint(Tutu, "o chapeu amassa kkkkkkk"));

            Say(Dev, "Esse é o limite: 1 metro. A grade de 0,8 m na parede já não dá nem agachado.");

            TutorialDoorGate.Open(GateDark);
        }

        private IEnumerator LessonFlashlight()
        {
            Func<bool> inside = () => TutorialZone.WasEntered(ZoneDarkRoom);
            Func<bool> lit = () => _flashlightUsed;
            Func<bool> out_ = () => TutorialZone.WasEntered(ZoneDarkExit);

            Step(3, "Lanterna", "Entre na sala escura");

            yield return WaitFor(inside, 15f,
                Hint(Dev, "A porta da próxima sala abriu. Pode entrar, não morde (ainda)."));

            SetBattery(darkRoomBattery);
            SetObjective($"Aperte {Key(TutorialKeys.Flashlight)} para ligar a lanterna");

            Teach(lit, Dev, $"Aperte {Key(TutorialKeys.Flashlight)} para ligar e desligar.");

            yield return WaitFor(lit, 10f,
                Hint(Dev, $"A tecla da lanterna é {Key(TutorialKeys.Flashlight)}."),
                Hint(Sono, "LIGA A LANTERNA"));


            TutorialDoorGate.Open(GateChat);
            SetObjective("Ache a saída da sala escura");

            Say(Tutu, "viu a barra da bateria? a lanterna gasta enquanto tá ligada. desliga quando não precisar");
            Teach(out_, Sono, "agora acha a saída. procura a luz verde em cima de uma porta");

            yield return WaitFor(() =>
                {
                    // Sem pilha aqui o jogador ficaria preso no escuro: a lição é achar a porta, não economizar.
                    if (GetBattery() < 8f) SetBattery(20f);
                    return out_();
                }, 25f,
                Hint(Tutu, "a saida ta na parede do fundo"),
                Hint(Dev, "Procure a luz verde em cima da porta, no fundo da sala, à direita."));
        }

        private IEnumerator LessonChat()
        {
            Func<bool> hidden = () => _chatHidden;
            Func<bool> shown = () => _chatPanel == null || _chatPanel.IsVisible;
            Func<bool> pressed = () => hiddenButton == null || hiddenButton.IsPressed;

            Step(4, "Chat", "Leia o chat");

            Say(Camis, "aqui chegam as mensagens da live. a galera reage ao que você filma e ao que acontece");

            if (_chatPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Chat)} para esconder o chat");
                Teach(hidden, Dev, $"Se o chat atrapalhar, aperte {Key(TutorialKeys.Chat)} para esconder. Tenta!");

                yield return WaitFor(hidden, 10f, Hint(Dev, $"A tecla é {Key(TutorialKeys.Chat)}."));

                SetObjective($"Aperte {Key(TutorialKeys.Chat)} de novo para trazer o chat de volta");

                // Com o chat fechado a linha fica esperando no painel.
                Teach(shown, Tutu, $"volta o chat! aperta {Key(TutorialKeys.Chat)} de novo, sem ele você perde as dicas");

                yield return WaitFor(shown, 8f,
                    Hint(Dev, $"Aperte {Key(TutorialKeys.Chat)} de novo! Sem o chat você perde as dicas."));

            }

            SetObjective("Ache o botão escondido que abre a próxima porta (o chat ajuda)");

            Teach(pressed, Dev, "Tem um botão escondido nesta sala que abre a porta. Se demorar, o chat dá dica!");

            yield return WaitFor(pressed, 10f, 9f,
                Hint(Sono, "procura atrás das coisas"),
                Hint(Sono, "olha atrás do armário!!"),
                Hint(Tutu, "ATRÁS DO ARMÁRIO DE METAL MANO, NO CANTO"),
                Hint(Dev, $"O pessoal tem razão: o botão está na parede atrás do armário de metal. Mire nele e use {Key(TutorialKeys.Interact)}."));

            Say(Dev, "Viu? Quando você empacar na partida, olhe o chat: alguém sempre sabe.");

            TutorialDoorGate.Open(GateItems);
        }

        private IEnumerator LessonItems()
        {
            Func<bool> picked = () => _batteryPicked;
            Func<bool> held = () => _batteryHeld || _batteryUsed;
            Func<bool> used = () => _batteryUsed;
            Func<bool> hovered = () => _blockedHovered;

            Step(5, "Itens e bateria", $"Mire na pilha em cima da mesa e use {Key(TutorialKeys.Interact)} para pegar");

            Teach(picked, Dev, "Itens! Quando você mira em algo que dá para usar, aparece o contorno e a tecla.");

            yield return WaitFor(picked, 14f,
                Hint(Dev, $"Chegue perto da mesa, mire na pilha (ela fica contornada) e use {Key(TutorialKeys.Interact)}."),
                Hint(Tutu, "pega a pilha"));

            SetObjective($"Aperte {Key(BatterySlotKey())} para segurar a pilha");

            Teach(held, Dev, "Ela foi para o inventário, aqui embaixo. São 4 espaços, nas teclas 1 a 4.");

            yield return WaitFor(held, 10f,
                Hint(Dev, $"Aperte o número do espaço onde a pilha está ({Key(BatterySlotKey())})."));

            SetObjective($"Use {Key(TutorialKeys.Use)} para colocar a pilha na lanterna");
            Teach(used, Dev, $"Com a pilha na mão, use {Key(TutorialKeys.Use)} para recarregar a lanterna. Olhe a barra!");

            // A pilha sai do inventário quando é usada.
            yield return WaitFor(used, 10f,
                Hint(Dev, $"Segurando a pilha, é só {Key(TutorialKeys.Use)}."));


            SetObjective("Mire na caixa em cima do case de metal");
            Teach(hovered, Dev, "Agora: nem tudo dá para pegar. Mire naquela caixa em cima do case, do lado da mesa.");

            yield return WaitFor(hovered, 12f,
                Hint(Dev, "A caixa de papelão em cima do case de metal. Só mire nela."));

            Say(Dev, "Contorno vermelho e INDISPONÍVEL: essa caixa é da missão de outro jogador. Só o dono pega.");

            TutorialDoorGate.Open(GateDoors);
        }

        private IEnumerator LessonDoors()
        {
            Func<bool> passed = () => TutorialZone.WasEntered(ZoneDoorsAfter);
            Func<bool> rattled = () => _doorRattled || passed();
            Func<bool> opened = () => _openDoorOpened || passed();
            Func<bool> closedBehind = () => passed() && TutorialZone.IsInside(ZoneDoorsAfter) && (openDoor == null || openDoor.IsClosed);

            Step(6, "Portas", $"Tente abrir a porta da esquerda ({Key(TutorialKeys.Interact)})");

            Teach(rattled, Dev, $"Portas abrem e fecham com {Key(TutorialKeys.Interact)}. Tente a da esquerda.");

            yield return WaitFor(rattled, 15f,
                Hint(Dev, $"A porta da esquerda. Mire e {Key(TutorialKeys.Interact)}."));

            Say(Dev, "Porta que chacoalha e não abre está sendo segurada pelo monstro (ou ele sabotou).");

            SetObjective($"Abra a porta da direita ({Key(TutorialKeys.Interact)})");
            Teach(opened, Camis, "vai pela da direita");

            yield return WaitFor(opened, 12f,
                Hint(Dev, $"Porta da direita, {Key(TutorialKeys.Interact)}."));

            SetObjective($"Passe e feche a porta atrás de você ({Key(TutorialKeys.Interact)})");

            Teach(closedBehind, Dev, "Ela abre para o lado oposto de quem abre. Agora passe e feche atrás de você.");

            yield return WaitFor(closedBehind, 14f,
                Hint(Dev, $"Do outro lado, vire para a porta e use {Key(TutorialKeys.Interact)} para fechar."),
                Hint(Sono, "fecha a porta!!"));

            Say(Dev, "Porta fechada segura o monstro um pouco: ele tem que forçar para passar.");
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

            _holdAudience = false;

            Step(7, "Audiência", "Grave um objeto com o olho: deixe ele na câmera até registrar");

            Teach(one, Tutu, "as coisas que o público quer ver têm um OLHO em cima. pra gravar é só deixar na câmera");
            Teach(one, Dev, "Olhe para um desses objetos por uns segundos e veja o que acontece com o olho.");

            yield return WaitFor(one, 18f,
                Hint(Dev, "Chegue perto (uns 5 metros) e mantenha o objeto no meio da tela por 3 segundos."),
                Hint(Tutu, "fica olhando pra coroa"));

            Say(Dev, "Viu o olho ficar VERMELHO? Quer dizer que a audiência daquele objeto entrou.");

            SetObjective("Grave mais um objeto diferente");
            Teach(two, Sono, "grava mais um, outro objeto");

            yield return WaitFor(two, 18f,
                Hint(Dev, "Outro objeto com olho: a espada ou a máscara."));

            Say(Dev, "Na partida, 75% de audiência é uma das chaves para abrir a saída.");

            TutorialDoorGate.Open(GatePuzzle);
        }

        private IEnumerator LessonPuzzle()
        {
            Func<bool> inside = () => TutorialZone.WasEntered(ZonePuzzleRoom);
            Func<bool> solved = () => puzzle == null || puzzle.IsComplete;
            Func<bool> panelHidden = () => _missionPanelHidden || solved();
            Func<bool> panelShown = () => _missionPanel == null || _missionPanel.IsVisible || solved();

            Step(8, "Missão e puzzle", "Entre na sala da missão");

            yield return WaitFor(inside, 15f,
                Hint(Dev, "A porta abriu. Vamos para a sua primeira missão."));

            StartPuzzle();

            Say(Dev, "Chegou missão! Ela aparece no painel de missões, no canto da tela.");
            Say(Camis, "toda missão tem um TÍTULO (o que é) e uma DICA (como resolver). lê com calma");

            if (_missionPanel != null)
            {
                SetObjective($"Aperte {Key(TutorialKeys.Missions)} para esconder e mostrar o painel de missões");

                Teach(panelHidden, Tutu, $"o painel também esconde: {Key(TutorialKeys.Missions)}");

                yield return WaitFor(panelHidden, 10f,
                    Hint(Dev, $"A tecla do painel de missões é {Key(TutorialKeys.Missions)}."));

                yield return WaitFor(panelShown, 8f,
                    Hint(Dev, $"{Key(TutorialKeys.Missions)} de novo para ver a missão."));
            }

            string title = puzzleSelector != null && puzzleSelector.Mission != null ? puzzleSelector.Mission.missionName : "a missão";
            string clue = puzzleSelector != null && puzzleSelector.Mission != null ? puzzleSelector.Mission.instructions : string.Empty;

            UpdatePuzzleObjective();

            Teach(solved, Dev, $"A missão é \"{title}\". A dica: \"{clue}\"");
            Teach(solved, Sono, "desalinhado = tem pedaço de cano virado errado. é a parede da direita: segue o cano de uma ponta até a outra");
            Teach(solved, Dev, $"Mire num pedaço de cano da parede e use {Key(TutorialKeys.Interact)} para girar. Gire até o caminho ficar inteiro.");

            yield return WaitFor(() =>
                {
                    UpdatePuzzleObjective();
                    return solved();
                }, 25f, 25f,
                Hint(Sono, "cada E gira um pouquinho, as vezes precisa de varias"),
                Hint(Camis, "começa numa ponta do cano e vai seguindo pedaço por pedaço até a outra"),
                Hint(Dev, "A faixa embaixo conta quantos pedaços estão no lugar. Um pedaço certo encaixa reto com o vizinho."),
                Hint(Tutu, "olha a mesa: tem pedaço de cano solto lá, mostra como é o encaixe"));


            Say(Dev, "Missão completa! Ela some do painel e o chat comemora.");
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

            TutorialDoorGate.Open(GateStage);
            SetObjective("Pare na marca do chão e grave o monstro no palco (monstro no meio da tela)");

            Teach(recorded, Dev, "MISSÃO PRINCIPAL: grave o monstro no palco, na próxima sala.");
            Say(Tutu, "calma, só aqui no tutorial ele tá congelado kkkk");
            Teach(recorded, Sono, "a dica fala em marca no chão: tem um X amarelo na frente do palco. grava dali!");

            yield return WaitFor(recorded, 25f,
                Hint(Dev, "Fique no X amarelo em frente ao palco e deixe o monstro no meio da tela."),
                Hint(Tutu, "filma ele no palco!!!"),
                Hint(Dev, "Chegue mais perto: o palco e o monstro precisam aparecer juntos na câmera, a menos de 10 metros."));

            StopMainMissionRecorder();


            Say(Dev, "GRAVADO! Na partida, missão principal completa + 75% de audiência abrem a porta da saída.");
        }

        private IEnumerator LessonDonation()
        {
            Step(10, "Doação", "Leia o pedido da doação");

            bool spawned = SpawnTutorialDonation();

            Say(Tutu, "OPA DOAÇÃO! o cartão no meio da tela é o pedido de quem doou");

            if (!spawned)
            {
                Say(Dev, "(Essa doação não chegou por algum problema. Seguimos sem ela.)");
                yield break;
            }

            SetObjective($"Cumpra a doação: grave o monstro por 5 segundos ({Key(TutorialKeys.Donate)} mostra os pedidos)");

            Teach(IsDonationComplete, Dev, $"Depois do aviso, o pedido fica num ícone embaixo das missões. Aperte {Key(TutorialKeys.Donate)} para ver de novo.");
            Teach(IsDonationComplete, Camis, "esse pede pra gravar o monstro por 5 segundos. a barrinha do cartão enche enquanto você grava");
            Say(Sono, "tem doação que pede pra GRITAR perto do monstro. aí é com o seu microfone. coragem kkkk");

            yield return WaitFor(IsDonationComplete, 20f,
                Hint(Dev, "Mantenha o monstro na câmera até a barrinha da doação encher."),
                Hint(Sono, "grava ele de novo"));

            Say(Dev, "Doação completa! Valeu demais.");
        }

        private IEnumerator LessonLights()
        {
            Func<bool> fixedLights = () => !IsLightsOut();

            Step(11, "Luzes", "A luz caiu! Ache o quadro de luz e use-o para religar");

            SetLightsOut(true);

            Say(Tutu, "ih... o que foi isso?");
            Teach(fixedLights, Dev, "O monstro pode apagar as luzes do teatro inteiro! Ligue a lanterna.");
            Teach(fixedLights, Dev, "Para religar, ache o QUADRO DE LUZ (uma caixa cinza na parede) e use.");

            yield return WaitFor(fixedLights, 18f,
                Hint(Tutu, "o quadro tá na parede do lado do palco"),
                Hint(Camis, $"liga a lanterna ({Key(TutorialKeys.Flashlight)}) e procura na parede da esquerda"),
                Hint(Dev, $"A caixa cinza com uma alavanca, na parede esquerda. Mire nela e use {Key(TutorialKeys.Interact)}."));

            Say(Dev, "Luz de volta! Na partida o quadro pode estar em outro lugar. O chat costuma lembrar onde.");
        }

        private IEnumerator LessonEscape()
        {
            Func<bool> started = () => TutorialZone.WasEntered(ZoneFugaStart);
            Func<bool> escaped = () => TutorialZone.WasEntered(ZoneExit);

            Step(12, "Fugir do monstro", $"Corra pela porta do fundo do palco até a SAÍDA! ({Key(TutorialKeys.Run)} corre)");

            OpenEscapeDoors();
            TutorialDoorGate.Open(GateFuga);

            Say(Dev, "Última lição: FUGIR. Na partida o monstro anda solto pelo teatro.");
            Say(Sono, "ele te acha pela visão e pelo barulho. correr, falar alto, porta e lanterna fazem barulho");
            Teach(started, Dev, "A porta do fundo do palco abriu. Quando você passar por ela, o monstro acorda. Corra até a SAÍDA!");

            yield return WaitFor(started, 12f,
                Hint(Dev, "Pela porta com a luz verde, no fundo do palco. Vai!"));

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
                Hint(Sono, "segue o corredor e vira a direita"),
                Hint(Dev, "Siga as placas verdes de SAÍDA."));

            _inFuga = false;

            if (monsterRemote != null) monsterRemote.SetPaused(true);

        }

        private void Finish()
        {
            _finished = true;

            SetObjective("Tutorial concluído!");

            Say(Dev, "VOCÊ SOBREVIVEU! Tutorial completo.");
            Say(Camis, $"lembrete: {Key(TutorialKeys.Pause)} pausa e tem as opções (volume, mouse, microfone)");
            Say(Tutu, "boa live e boa sorte!!");

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
            SayNow(Tutu, "essa porta abre sozinha quando a luz de cima ficar VERDE. termina o que tá na faixa de baixo");
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
                SayNow(Dev, "Ufa! Aqui no tutorial você não morre. Na partida, cuidado!");
                yield break;
            }

            if (_hitsTaken < 3)
            {
                SayNow(Dev, "Ele te pegou! Aqui eu te curo, mas na partida cada golpe tira vida. CORRE!");
                yield break;
            }

            _hitsTaken = 0;

            SayNow(Dev, "Vamos de novo! Corra assim que entrar no corredor e feche as portas atrás de você.");

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

        /// <summary>Dica dita por <paramref name="viewer"/> (sempre alguém da turma que ensina).</summary>
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

        /// <summary>Fala na fila: sai no ritmo de leitura, ninguém espera por ela.</summary>
        private void Say(string who, string text) => Enqueue(who, text, null);

        /// <summary>
        /// Fala que ensina uma ação: se o jogador já fez (<paramref name="done"/>) quando chegar a vez
        /// dela, ela é descartada.
        /// </summary>
        private void Teach(Func<bool> done, string who, string text) => Enqueue(who, text, done);

        /// <summary>Aviso que não pode esperar a fila (golpe do monstro, porta de lição): sai agora.</summary>
        private void SayNow(string who, string text)
        {
            // Entra logo depois do resto da fala que já começou (não corta ninguém no meio).
            int at = 0;
            while (at < _speech.Count && !_speech[at].First) at++;

            List<string> parts = SplitForChat(who, text);
            int group = ++_lineGroup;

            for (int i = 0; i < parts.Count; i++) _speech.Insert(at + i, new Line(who, parts[i], null, _step, group, i == 0));

            // Urgente, mas não colado na mensagem anterior: dá pelo menos 2 s para ler.
            if (at == 0) _nextLineTime = Mathf.Min(_nextLineTime, Mathf.Max(Time.time, _lastPostTime + 2f));
        }

        private void Enqueue(string who, string text, Func<bool> skip)
        {
            List<string> parts = SplitForChat(who, text);
            int group = ++_lineGroup;

            for (int i = 0; i < parts.Count; i++) _speech.Add(new Line(who, parts[i], skip, _step, group, i == 0));
        }

        private void Speak()
        {
            if (Time.time < _nextLineTime) return;

            while (_speech.Count > 0)
            {
                Line line = _speech[0];
                _speech.RemoveAt(0);

                // A fala inteira sai ou é descartada inteira: a decisão é na primeira parte.
                // Descartada se a ação já foi feita, ou se é comentário de duas lições atrás (o
                // fechamento de uma lição sai já na seguinte, por isso a folga de uma).
                bool stale = line.Skip != null ? line.Skip() : line.Step < _step - 1 && !_finished;

                if (line.First && stale)
                {
                    while (_speech.Count > 0 && !_speech[0].First && _speech[0].Group == line.Group) _speech.RemoveAt(0);
                    continue;
                }

                Post(line.Who, line.Text);
                _lastPostTime = Time.time;

                // Quem está começando lê devagar: tempo mínimo por mensagem, mais por letra.
                _nextLineTime = Time.time + Mathf.Max(minLineSeconds, VisibleLength(line.Text) * secondsPerCharacter);
                return;
            }
        }

        private static void Post(string viewer, string text) => TutorialChat.Post(viewer, text);

        // O ChatUi encolhe a fonte quando o texto não cabe em duas linhas: com o nome junto, até uns
        // 45 caracteres a mensagem sai no tamanho cheio. Mais que isso vira mais de uma mensagem.
        private const int ChatLineBudget = 46;

        /// <summary>
        /// Quebra a fala em mensagens que caibam no chat sem a fonte encolher. Corta só entre palavras
        /// (nunca dentro de uma tag de tecla) e, quando dá, depois de um ponto ou vírgula.
        /// </summary>
        private static List<string> SplitForChat(string who, string text)
        {
            int budget = Mathf.Max(22, ChatLineBudget - who.Length - 2);
            List<string> parts = new();

            if (VisibleLength(text) <= budget)
            {
                parts.Add(text);
                return parts;
            }

            // 1) Uma frase por mensagem; 2) frase longa vira partes de tamanho parecido, cortando de
            // preferência na vírgula; 3) frases curtas vizinhas voltam a se juntar se couberem.
            List<string> pieces = new();

            foreach (List<string> sentence in Sentences(Words(text)))
            {
                string joined = string.Join(" ", sentence);

                if (VisibleLength(joined) <= budget) pieces.Add(joined);
                else pieces.AddRange(Balanced(sentence, budget));
            }

            foreach (string piece in pieces)
            {
                int last = parts.Count - 1;

                if (last >= 0 && VisibleLength(parts[last] + " " + piece) <= budget) parts[last] += " " + piece;
                else parts.Add(piece);
            }

            return parts;
        }

        /// <summary>Palavras separadas por espaço fora de tags (a tecla com cor é uma palavra só).</summary>
        private static List<string> Words(string text)
        {
            List<string> words = new();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') depth++;
                else if (c == '>') depth = Mathf.Max(0, depth - 1);
                else if (c == ' ' && depth == 0)
                {
                    if (i > start) words.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }

            if (start < text.Length) words.Add(text.Substring(start));
            return words;
        }

        private static IEnumerable<List<string>> Sentences(List<string> words)
        {
            List<string> current = new();

            foreach (string word in words)
            {
                current.Add(word);

                char end = VisibleTail(word);
                if (end == '.' || end == '!' || end == '?')
                {
                    yield return current;
                    current = new List<string>();
                }
            }

            if (current.Count > 0) yield return current;
        }

        /// <summary>Divide em partes de tamanho parecido (nenhum pedaço órfão no fim), preferindo a vírgula.</summary>
        private static List<string> Balanced(List<string> words, int budget)
        {
            int total = VisibleLength(string.Join(" ", words));
            List<string> result = null;

            // Se alguma parte estourar o limite, tenta com uma parte a mais.
            for (int count = Mathf.CeilToInt(total / (float)budget); count <= words.Count; count++)
            {
                result = BalancedInto(words, budget, count);
                if (result.TrueForAll(part => VisibleLength(part) <= budget)) break;
            }

            return result;
        }

        private static List<string> BalancedInto(List<string> words, int budget, int count)
        {
            List<string> result = new();
            int index = 0;

            for (int part = 0; part < count && index < words.Count; part++)
            {
                if (part == count - 1)
                {
                    result.Add(string.Join(" ", words.GetRange(index, words.Count - index)));
                    break;
                }

                int remaining = VisibleLength(string.Join(" ", words.GetRange(index, words.Count - index)));
                float target = remaining / (float)(count - part);

                int bestEnd = index;
                float bestScore = float.MaxValue;
                int length = -1;

                for (int j = index; j < words.Count - 1; j++)
                {
                    length += 1 + VisibleLength(words[j]);
                    if (length > budget && j > index) break;

                    float score = Mathf.Abs(length - target);
                    if (VisibleTail(words[j]) == ',' || VisibleTail(words[j]) == ':') score -= 8f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestEnd = j;
                    }
                }

                result.Add(string.Join(" ", words.GetRange(index, bestEnd - index + 1)));
                index = bestEnd + 1;
            }

            return result;
        }

        private static char VisibleTail(string word)
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(word, "<[^>]+>", string.Empty);
            return plain.Length > 0 ? plain[plain.Length - 1] : ' ';
        }

        private static int VisibleLength(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty).Length;

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
        /// manda a próxima dica pela fila; depois uma a cada <paramref name="repeat"/> segundos, a
        /// última se repetindo. A dica que chegar depois de a ação estar feita é descartada.
        /// </summary>
        private IEnumerator WaitFor(Func<bool> done, float firstHint, float repeat, params ChatHint[] hints)
        {
            float timer = 0f;
            int next = 0;

            while (!done())
            {
                if (_speech.Count == 0 && Time.time >= _nextLineTime) timer += Time.deltaTime;

                float threshold = next == 0 ? firstHint : repeat;

                if (hints != null && hints.Length > 0 && timer >= threshold)
                {
                    timer = 0f;
                    ChatHint hint = hints[Mathf.Min(next, hints.Length - 1)];
                    Enqueue(hint.Viewer, hint.Text, done);
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
