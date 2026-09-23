# Build para LAN bloqueada — diagnóstico e correções

Cenário: rede com internet, mas com bloqueio de saída que impede alcançar servidores externos
(Relay, Lobby, Vivox, e possivelmente a própria autenticação da Unity).

**Conclusão em uma linha:** o modo LAN já está implementado e não depende de nenhum serviço —
o que quebra é o *portão de boot*, que exige autenticação bem-sucedida antes de carregar o menu.

---

## 1. A cadeia de boot

```
Bootstrap.unity           NameSelector — grava o nome no PlayerPrefs
        │                 SceneManager.LoadScene(buildIndex + 1)
        ▼
Netbootstrap.unity        ApplicationController.Start()
        │
        ├── Instantiate(HostManager.prefab)      → VivoxManager.Start()  [!] toca VivoxService
        │                                          Lobby (UGS)
        ├── Instantiate(ClientSingleton)
        │
        └── await CreateClient()
                └── ClientGameManager.InitAsync()
                      ├── await UnityServices.InitializeAsync()            REDE (sem try/catch)
                      └── await AuthenticationController.Authenticate(5)
                            ├── await AuthenticationService.SignInAnonymouslyAsync()   REDE
                            └── await VivoxService.Instance.InitializeAsync()          REDE
                                                                                       [!] mesmo try
        if (authenticated)  ──►  StartMenu()  ──►  MainMenu.unity  (onde vive o painel LAN)
        └── se falhar: NADA ACONTECE. Fica parado na Netbootstrap para sempre.
```

O painel LAN fica na `MainMenu.unity`. Se a autenticação falhar, essa cena nunca carrega, e o
modo LAN — que não precisa de serviço nenhum — se torna inalcançável.

---

## 2. Problemas, por severidade

### P1 (crítico) — O menu só carrega se a autenticação passar

`Assets/Scripts/Network/ApplicationController.cs:34`

```csharp
if (authenticated)
{
    clientSingletonObject.gameManager.StartMenu();
}
```

Sem `else`. Serviço fora = tela parada, sem mensagem, sem timeout, sem saída.
**Este item sozinho impede a build de rodar na faculdade.**

### P2 (crítico) — Falha do Vivox derruba o jogo inteiro

`Assets/Scripts/Network/AuthenticationController.cs:44`

```csharp
await AuthenticationService.Instance.SignInAnonymouslyAsync();
await VivoxService.Instance.InitializeAsync();     // <- mesmo try
```

O Vivox usa servidores e portas próprios, e é o candidato mais provável a estar bloqueado.
Se o login passar mas o Vivox falhar, o `catch` marca `Error` -> `Timeout` -> `authenticated = false`
-> cai no P1. **Voz quebrada = jogo não abre.**

### P3 (crítico) — Exceções fora dos dois `catch` escapam

`AuthenticationController` só captura `AuthenticationException` e `RequestFailedException`.
`ServicesInitializationException`, `TimeoutException`, `HttpRequestException` e
`OperationCanceledException` passam direto. E `UnityServices.InitializeAsync()` em
`ClientGameManager.InitAsync():23` não tem try/catch nenhum.

Sobem até o `catch (Exception)` do `ApplicationController`, viram um `Debug.Log` e o
`StartMenu()` nunca roda. Mesmo resultado do P1, por outro caminho.

### P4 (alto) — Sem timeout: tela preta por tempo indefinido

Firewall que faz DROP (descarta em silêncio) em vez de REJECT deixa o TCP em retry.
`UnityServices.InitializeAsync()` e `SignInAnonymouslyAsync()` podem demorar dezenas de segundos
cada antes de desistir. Não há `CancellationToken` nem corrida com `Task.Delay` em lugar nenhum
do caminho de boot.

### P5 (alto) — O retry não faz retry

`AuthenticationController.cs:38`

```csharp
while (CurrentAuthentaticationState == AuthenticationState.Authenticating && triesCounter < maxTries)
{
    try { ... }
    catch (...) { CurrentAuthentaticationState = AuthenticationState.Error; }   // <- sai do while
    triesCounter++;
    await Task.Delay(1000);
}
```

O `catch` altera a variável que o `while` testa. Na primeira exceção o laço termina.
`MAX_TRIES_TO_AUTH = 5` é código morto. Não é fatal, mas você acha que tem 5 tentativas e tem 1.

### P6 (alto) — `VivoxManager` toca o serviço sem guarda

`Assets/Scripts/Network/VivoxManager.cs` — `Start()` e `OnDisable()`

```csharp
VivoxService.Instance.LoggedIn += VivoxService_OnUserLoggedIn;
// ... mais 5 linhas iguais
Lobby.instance.OnJoinedLobby += VivoxService_OnJoinedLobby;
```

Nenhum null-check. `VivoxManager` vive no `HostManager.prefab`, que é instanciado no boot
**antes** de `UnityServices.InitializeAsync()` terminar. Sem serviços inicializados,
`VivoxService.Instance` lança ou devolve null (varia com a versão) — em qualquer um dos dois
casos o `Start()` aborta no meio e o objeto fica meio-inscrito. Como é `DontDestroyOnLoad`,
o estado quebrado acompanha a sessão inteira.

### P7 (crítico) — `PlayerVoiceIdentity` quebra todo spawn de player

`Assets/Scripts/Network/PlayerVoiceIdentity.cs:20`

```csharp
public override void OnNetworkSpawn()
{
    if (!IsOwner) return;
    _vivoxPlayerId.Value = AuthenticationService.Instance.PlayerId;   // <- sem guarda
}
```

Este componente está no `GFX` do `Player.prefab`. Sem serviços, `AuthenticationService.Instance`
lança — **em toda entrada de player, em partida LAN**. Este não trava o boot: trava o jogo.

### P8 (médio) — `RemoteVoiceFilter` tem uma guarda faltando

`Assets/Scripts/Audio/RemoteVoiceFilter.cs:219`

```csharp
foreach (VivoxParticipant participant in VivoxManager.instance.CurrentParticipants)
```

O resto do arquivo checa `VivoxManager.instance != null`; esta linha não. E `CurrentParticipants`
acessa `VivoxService.Instance.ActiveChannels` por dentro.

### P9 (médio) — Connect LAN demora 60s para desistir

`Assets/Scenes/Netbootstrap.unity`, componente `UnityTransport`:

```
m_ConnectTimeoutMS: 1000
m_MaxConnectAttempts: 60      ->  60 segundos
m_DisconnectTimeoutMS: 30000
```

IP errado, porta fechada ou firewall entre as máquinas = um minuto de silêncio antes de
`ConnectionFeedback` dizer qualquer coisa. Na prática os jogadores vão achar que travou.

### P10 — O que já está correto

| Item | Estado |
|---|---|
| `m_ProtocolType: 0` (UnityTransport, não RelayUnityTransport) | OK — LAN funciona |
| `Lan.cs` -> `StartLanHostAsync` / `StartLanClientAsync` | OK — zero serviços |
| Host LAN escuta em `0.0.0.0` | OK — aceita Wi-Fi, Ethernet, VPN |
| `ConnectionPayload.ResolveAuthId()` | OK — já cai para GUID local |
| `PlayerTracker` | OK — já deriva o total de players em LAN |
| `PlayersReady` (lobby da partida) | OK — sem dependência de serviço |
| `Lobby.HeartBeat` / `LobbyPullForUpdate` | OK — saem cedo quando não há lobby UGS |
| `PauseUi` | OK — null-check em `VivoxManager` e `Lobby` |
| `PlayerMicReporter` | OK — null-checks, desabilita se não for owner |
| Analytics, Ads, Purchasing, Crash Reporting | OK — desligados em `UnityConnectSettings` |

---

## 3. Correções

### 3.1 Flag global de disponibilidade

Arquivo novo, `Assets/Scripts/Network/OnlineServices.cs`:

```csharp
namespace Network
{
    /// <summary>
    /// Se os serviços da Unity (auth, Relay, Lobby, Vivox) responderam no boot.
    /// Falso numa rede que bloqueia saída — o jogo segue em LAN.
    /// </summary>
    public static class OnlineServices
    {
        public static bool IsAvailable { get; set; }
        public static bool IsVoiceAvailable { get; set; }
    }
}
```

### 3.2 O menu sempre carrega — `ApplicationController.cs`

```csharp
private async Task LaunchClientAndHost()
{
    HostSingleton hostSingletonObject = Instantiate(hostSingleton);
    hostSingletonObject.CreateHost();

    ClientSingleton clientSingletonObject = Instantiate(clientSingleton);

    bool authenticated = false;

    try
    {
        authenticated = await clientSingletonObject.CreateClient();
    }
    catch (Exception e)
    {
        Debug.LogWarning($"Serviços indisponíveis; seguindo apenas com LAN. {e.Message}");
    }

    OnlineServices.IsAvailable = authenticated;

    // Sempre. O modo LAN não precisa de serviço nenhum, e o painel dele vive no menu.
    clientSingletonObject.gameManager.StartMenu();
}
```

### 3.3 Timeout duro — `ClientGameManager.InitAsync()`

```csharp
private const int SERVICES_TIMEOUT_MS = 8000;

public async Task<bool> InitAsync()
{
    // Não depende de serviço; precisa existir mesmo offline.
    _networkClientManager = new NetworkClientManager(NetworkManager.Singleton);

    try
    {
        Task init = UnityServices.InitializeAsync();

        if (await Task.WhenAny(init, Task.Delay(SERVICES_TIMEOUT_MS)) != init)
        {
            Debug.LogWarning("UnityServices.InitializeAsync não respondeu a tempo. Modo offline.");
            return false;
        }

        await init;   // propaga a exceção, se houve
    }
    catch (Exception e)
    {
        Debug.LogWarning($"UnityServices indisponível: {e.Message}");
        return false;
    }

    Task<AuthenticationState> auth = AuthenticationController.Authenticate(MAX_TRIES_TO_AUTH);

    if (await Task.WhenAny(auth, Task.Delay(SERVICES_TIMEOUT_MS)) != auth)
    {
        Debug.LogWarning("Autenticação não respondeu a tempo. Modo offline.");
        return false;
    }

    return await auth == AuthenticationState.Authenticated;
}
```

### 3.4 Vivox fora do caminho crítico — `AuthenticationController.cs`

```csharp
while (CurrentAuthentaticationState == AuthenticationState.Authenticating && triesCounter < maxTries)
{
    try
    {
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        if (AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
        {
            CurrentAuthentaticationState = AuthenticationState.Authenticated;
            await TryInitializeVoiceAsync();   // falha aqui não derruba a autenticação
            break;
        }
    }
    catch (Exception ex)   // era só AuthenticationException + RequestFailedException
    {
        Debug.LogWarning($"Tentativa {triesCounter + 1}/{maxTries} de autenticação falhou: {ex.Message}");
        // NÃO escrever em CurrentAuthentaticationState aqui: é o que o while testa.
    }

    triesCounter++;
    await Task.Delay(1000);
}

if (CurrentAuthentaticationState != AuthenticationState.Authenticated)
{
    CurrentAuthentaticationState = AuthenticationState.Timeout;
    Debug.LogWarning("Autenticação não concluída — o jogo segue em modo LAN.");
}

private static async Task TryInitializeVoiceAsync()
{
    try
    {
        await VivoxService.Instance.InitializeAsync();
        OnlineServices.IsVoiceAvailable = true;
    }
    catch (Exception ex)
    {
        OnlineServices.IsVoiceAvailable = false;
        Debug.LogWarning($"Vivox indisponível; o jogo segue sem voz. {ex.Message}");
    }
}
```

Tirar a escrita de `Error` de dentro do `catch` é o que faz o `maxTries` voltar a valer (P5).

### 3.5 Guardas — `VivoxManager.cs`

```csharp
private bool _wired;

private void Start()
{
    if (OnlineServices.IsVoiceAvailable)
    {
        try
        {
            VivoxService.Instance.LoggedIn  += VivoxService_OnUserLoggedIn;
            VivoxService.Instance.LoggedOut += VivoxService_OnUserLoggedOut;
            VivoxService.Instance.ChannelJoined += VivoxService_OnChannelJoined;
            VivoxService.Instance.ChannelLeft   += VivoxService_OnChannelLeft;
            VivoxService.Instance.ParticipantAddedToChannel     += VivoxService_OnParticipantAddedToChannel;
            VivoxService.Instance.ParticipantRemovedFromChannel += VivoxService_OnParticipantRemovedFromChannel;
            _wired = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Vivox indisponível; voz desligada nesta sessão. {e.Message}");
            _wired = false;
        }
    }

    if (Lobby.instance != null)
    {
        Lobby.instance.OnJoinedLobby += VivoxService_OnJoinedLobby;
        Lobby.instance.OnLeftLobby   += VivoxService_OnLeftLobby;
    }

    Application.quitting += Application_ApplicationQuit;
}
```

No `OnDisable()`, o bloco do `VivoxService` vai dentro de `if (_wired)` + try/catch, e o trecho
do `Lobby.instance` dentro de `if (Lobby.instance != null)`.

Os métodos `async void` que chamam `EnsureLoggedInAsync()` (`EnterLobbyVoice`, `EnterGameVoice`,
`EnterTestVoiceChannel`) também devem sair cedo com `if (!_wired) return;`.

### 3.6 Guarda — `PlayerVoiceIdentity.cs`

```csharp
public override void OnNetworkSpawn()
{
    if (!IsOwner) return;

    _vivoxPlayerId.Value = SafePlayerId();
}

private static string SafePlayerId()
{
    try
    {
        if (AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn)
        {
            return AuthenticationService.Instance.PlayerId;
        }
    }
    catch (Exception)
    {
        // Sem serviços: identidade de voz não existe, e não precisa existir em LAN.
    }

    return string.Empty;
}
```

Mesmo padrão que o `ConnectionPayload.ResolveAuthId()` já usa.

### 3.7 Guarda — `RemoteVoiceFilter.cs:219`

```csharp
if (VivoxManager.instance == null) return;

foreach (VivoxParticipant participant in VivoxManager.instance.CurrentParticipants)
```

### 3.8 Transporte responde mais rápido

`Netbootstrap.unity` -> `NetworkManager` -> `UnityTransport`:

| Campo | De | Para |
|---|---|---|
| Max Connect Attempts | 60 | **10** |
| Connect Timeout MS | 1000 | 1000 |

10 segundos e o `ConnectionFeedback` fala. Não mexa no `DisconnectTimeoutMS` (30s) — esse é
tolerância a lag em partida, não a connect.

### 3.9 Menu honesto sobre o que está disponível

Em `MainMenu.Start()`:

```csharp
if (!OnlineServices.IsAvailable)
{
    onlineButton.interactable = false;
    ConnectionFeedback.Report("Servidores indisponíveis nesta rede. Use o modo LAN.");
}
```

Precisa expor o botão Online como `[SerializeField]` — hoje `MainMenu.cs` só guarda os painéis.

---

## 4. Checklist no dia, na faculdade

### Antes de sair de casa

- [ ] Build com as correções da seção 3
- [ ] Testar a build **com a internet desligada**: tem que chegar no menu, com o botão Online
      desabilitado e o LAN funcionando
- [ ] Testar LAN com duas máquinas na sua rede de casa
- [ ] Anotar a porta escolhida (o campo é digitado na UI; padrão do transporte é `7777`)
- [ ] Levar um roteador/switch próprio como plano B — resolve tudo se a rede da faculdade
      isolar as máquinas entre si

### No laboratório

1. **Descobrir o IP do host** — `ipconfig` no cmd, pegar o IPv4 do adaptador ativo.
   Se começar com `169.254.` não há DHCP: não vai funcionar.

2. **Confirmar que as máquinas se enxergam** — do cliente:
   ```
   ping <ip-do-host>
   ```
   Se o ping falhar, o problema é isolamento de cliente na rede, e nenhuma mudança no jogo
   resolve. É aí que entra o roteador próprio.

3. **Liberar a porta no host** (precisa de admin):
   ```
   netsh advfirewall firewall add rule name="TCC LAN" dir=in action=allow protocol=UDP localport=7777
   ```
   Sem admin, tente hospedar e aceite o prompt do Windows Firewall que aparece no primeiro
   `StartHost`. Se o prompt não aparecer e a conexão falhar, a política da máquina já negou.

4. **Confirmar que o host está escutando**:
   ```
   netstat -an | findstr 7777
   ```
   Tem que aparecer `UDP  0.0.0.0:7777`.

5. **UnityTransport é UDP.** Algumas redes gerenciadas liberam TCP e bloqueiam UDP entre hosts.
   Se o ping passa mas a conexão não fecha, é esse o motivo — e a saída é o roteador próprio.

### O que esperar de degradação com os serviços bloqueados

| Recurso | Em LAN sem serviços |
|---|---|
| Hospedar e entrar por IP | funciona |
| Gameplay, missões, monstro, audiência, chat de viewers | funciona (tudo é NGO puro) |
| Nome do jogador | funciona (vem do PlayerPrefs) |
| Voz (Vivox) | silenciosa — aceitável |
| TTS das doações | silencioso (passa pelo Vivox) |
| Criar/entrar em sala Online | botão desabilitado |

O chat de viewers, as doações e a audiência **não** passam por serviço externo — são simulados
localmente e replicados por NGO. Continuam funcionando.
