# Voz (Vivox) e som — panorama, diagnóstico e onde ajustar

**Data:** 2026-11-01 · **Branch:** `BugsSolutions` (commit `beb6a051`)
**O que é:** como a voz entre jogadores funciona hoje, onde fica cada ajuste, o diagnóstico dos 5
problemas relatados e o laboratório de voz no GYM para ouvir tudo em situação real.
**O que não é:** nenhuma correção foi aplicada no jogo. Os scripts, prefabs e cenas do jogo estão
intactos; tudo o que foi criado está em `Assets/GYM/VoiceLab/`, `Assets/GYM/Editor/VoiceLab/` e
`Assets/GYM/Scenes/GYM_VozLab.unity`.

---

## 1. Como a voz anda hoje

```
Microfone do jogador A
  └─ Vivox (login + canal)                         VivoxManager  (HostManager.prefab, cena Netbootstrap)
       ├─ lobby  : canal 2D  "<lobbyId>_lobby"     TextAndAudio, sem posição
       ├─ partida: canal 3D  "<lobbyId>_game"      AudioOnly, posição enviada por VivoxPlayer.Update
       └─ teste  : canal de eco "MicTestChannel"   troca o canal da partida pelo eco (sai do 3D!)

No computador do jogador B, para cada jogador remoto (RemoteVoiceFilter no Player.prefab):
  tap do participante (silenciado no mix do Vivox)  → AudioSource 3D no VoiceAnchor (boca)
    → Steam Audio (HRTF, distância pela curva do AudioSource, ar, oclusão/transmissão, diretividade)
    → VoiceGain (volume que B escolheu no pause)
    → AudioReverbFilter (reverb da zona onde A está)
    → mixer VoiceSoundMixer / Voice → Master (voiceVolume)  → saída

Volume do AudioSource = "janela" que fecha a voz no alcance atual; o alcance sobe e desce com a
energia do microfone de A (sussurro 8 m … grito 32 m).

Outros caminhos:
  monstro ouvindo  : PlayerMicReporter (energia do próprio mic → NoiseBus, 10 m fala / 24 m grito)
  lista do pause   : PlayerListInGameUi  → participantes do canal Vivox
  ícone de voz     : PlayerVoiceIndicator → VivoxParticipant.SpeechDetected
  TTS das doações  : DonationTTSManager → TTS do Vivox enviado PELA VOZ do destinatário
```

---

## 2. Onde ajustar cada coisa

| O quê | Onde | Hoje | O que muda |
|---|---|---|---|
| Alcance do grito (e da leitura de doação) | `Assets/Prefabs/Network/HostManager.prefab` → VivoxManager → **Audible Distance** | 32 | Raio onde a voz chega a zero no grito (`LoudRange`). |
| Alcance do canal Vivox (quem fica na lista) | VivoxManager → **Channel Audible Distance** (não aparece no YAML, vale o padrão do código) | 1000 | Passou disso o Vivox tira a pessoa da lista. Tem que ser igual em todo build (vai no nome do canal: `!p-1000-3-1.000-3`). |
| Fade do próprio Vivox | VivoxManager → Conversational Distance / Audio Fade Intensity / Audio Fade Model | 3 / 1 / Exponential | Só vale para o que o Vivox toca sozinho: lobby, eco e vozes sem tap (ver 3.3). |
| AGC (nivelar volume dos mics) | VivoxManager → Automatic Gain Control | desligado | Ligado, grito e fala saem quase iguais e o alcance por voz para de fazer sentido. |
| Limiares sussurro / fala / grito | `Assets/Scriptable_objects/Sounds/VoiceProfile.asset` | fala 0,25 · grito 0,65 · “falando” 0,65 | Energia do Vivox (0..1) que define o alcance e o que o monstro ouve. |
| Alcance do sussurro / campo próximo | VoiceProfile.asset → Whisper Range / Near Field | 8 m / 1 m | Alcance mínimo e onde começa a perda de 6 dB por dobra de distância. |
| Velocidade do alcance | `Assets/Prefabs/Player/Player.prefab` → RemoteVoiceFilter → Range Attack / Release, Range Scales With Voice | 0,08 s / 0,9 s / ligado | Com 0,9 s o alcance despenca entre as sílabas. |
| Espacialização da voz | `Assets/Scriptable_objects/Sounds/Spatial/VoiceSpatialProfile.asset` | oclusão on, Source Radius 0,25, transmissão on (2 superfícies), diretividade 0,3, ar on | Paredes, “vazamento” pelo vão da porta, perda de costas (0,3 ≈ −8 dB). |
| Quanto passa pelas paredes | `Assets/Scriptable_objects/Sounds/Acoustics/Wall.asset` (material padrão do Steam Audio) | transmissão 0,056 / 0,056 / 0,004 | ≈ −25 dB e quase sem agudos atrás de uma parede. Vale para TODOS os sons. |
| Quanto passa pelas portas | `Assets/Scriptable_objects/Sounds/Acoustics/Door.asset` | 0,25 / 0,1 / 0,04 | ≈ −20 dB nos médios com a porta fechada. |
| Quais camadas bloqueiam som | `Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset` → Layer Mask | Default, Walls, Doors | Props e Ground não abafam. |
| Volume geral das vozes | `Assets/Sounds/VoiceSoundMixer.mixer` → Master (parâmetro exposto `voiceVolume`) | 0 dB, **ninguém usa** | Não existe slider de voz no Options. |
| Volume de cada jogador | Pause → lista → slider (PlayerListItemUi → VivoxManager.SetParticipantVolume) | 0 (−50..50) | Vai para o VoiceGain do tap (até +6 dB com soft clip). |
| Volume do TTS para quem recebe a doação | Options → Testar microfone → **Volume de saída** (AudioDeviceSettings → `SetOutputDeviceVolume`) | 0, não é salvo | Único controle do TTS local hoje (também mexe no lobby e no eco). |
| Voz do TTS | `Assets/Prefabs/Managers/DonationAudioManager.prefab` → DonationTTSManager → Tts Voice | `en_US female` | Voz em inglês lendo texto em português. |
| Duração da leitura (alcance cheio) | DonationTTSManager → Words Per Second / Reading Slack | 2,5 / 1,5 s | Enquanto “lendo”, a voz do destinatário vai a 32 m. |
| Monstro ouvindo a voz | Player.prefab → PlayerMicReporter → Speech/Shout Loudness, Report Interval | 10 m / 24 m / 0,35 s | Independe do que os jogadores ouvem. |

---

## 3. Diagnóstico dos 5 pontos

### 3.1 Volume x distância: “saio da sala e já não ouço”

Medido no laboratório (seção 5): **a fala normal some por volta de 10–12 m mesmo num corredor reto
sem nada no caminho**, e cai mais uns 25 dB no instante em que uma parede entra entre vocês. Não é
um problema só: são cinco perdas empilhadas.

1. **O alcance pulsa com cada sílaba.** `RemoteVoiceFilter.Update`
   (`Assets/Scripts/Audio/RemoteVoiceFilter.cs:153-191`) leva o alcance de 8 m (sussurro) a 32 m
   (grito) conforme a energia do mic, que o Vivox atualiza 5×/s e que cai a quase zero entre as
   palavras. Com release de 0,9 s para o vão inteiro (24 m), o alcance desce ~27 m/s nas pausas: na
   medição com fala normal ele ficou entre 8 e 22 m, em média 12–13 m.
2. **A “janela” age no alcance inteiro.** `ApplyRange` (`RemoteVoiceFilter.cs:201-232`) multiplica
   por (1 − x²)², x = distância/alcance: já tira 3 dB a 40% do alcance, 8 dB a 60% e 18 dB a 80%.
   Somado ao item 1, a borda fica sempre perto de você.
3. **Lei 1/d desde 1 m.** Logarithmic com Min Distance = Near Field (1 m) (`RemoteVoiceFilter.cs:106,123`
   e VoiceProfile.asset): −12 dB a 4 m, −18 dB a 8 m, −24 dB a 16 m. É fisicamente correto em campo
   livre, mas um corredor de verdade tem reverberação que segura o nível — aqui não tem.
4. **Paredes viram degrau, não rampa.** Steam Audio com oclusão volumétrica de 0,25 m e transmissão
   pelo material Wall (≈ −25 dB). Reflexões e *pathing* estão desligados
   (`Assets/Scripts/Components/Sound/SpatialAudioProfileSO.cs:100-101`), então o som não contorna o
   batente: no passo em que a linha reta até a boca encosta na parede, perde ~25 dB de uma vez.
5. **De costas, −8 dB.** Diretividade 0,3 no VoiceSpatialProfile: quem sai da sala andando e falando
   fica de costas para você.

**Proposta (já tocável no laboratório, tecla I):**

| Ajuste | Hoje | Proposta | Onde mudar no jogo |
|---|---|---|---|
| Campo próximo | 1 m, −6 dB/dobra | 2 m, −4,5 dB/dobra | VoiceProfile.Near Field + curva custom no RemoteVoiceFilter (hoje é Logarithmic fixo) |
| Alcance | sussurro 8 → grito 32, segue cada sílaba | sussurro 8 · **fala 24** · grito 40, com *hold* de 1,5 s enquanto houver fala | RemoteVoiceFilter.Update + novo campo “Speech Range” no VoiceProfile; Audible Distance 40 |
| Release do alcance | 0,9 s | 2,5 s | Player.prefab → RemoteVoiceFilter → Range Release Seconds |
| Borda | (1 − x²)² no alcance inteiro | só nos últimos 40% (cosseno) | RemoteVoiceFilter.ApplyRange |
| Vazamento na porta | Source Radius 0,25 m | 1,0 m | VoiceSpatialProfile.asset (só dado) |
| Atrás da parede | ≈ −25 dB, sem agudos | piso de ≈ −16 dB (abafado, não mudo) | Valor mínimo de transmissão para vozes (SpatialAudioProfileSO) — não mexer no Wall.asset, que vale para todos os sons |
| De costas | −8 dB | −3,1 dB (diretividade 0,15) | VoiceSpatialProfile.asset (só dado) |

Dá para começar só com dados (sem código): VoiceSpatialProfile (Source Radius 1,0, Directivity
0,15), Range Release 2,5 s e Audible Distance ~40. O resto pede mexer no RemoteVoiceFilter.

### 3.2 Menu principal sem botão de testar o microfone

O Options do menu é uma instância do `OptionsMenu.prefab` dentro de `Assets/Scenes/MainMenu.unity`
com overrides feitos em 2026-08-24 (commit `3ca95196`, “Adjusts Options Menu”):

- `Panel/AudioDeviceButton` **desligado** (no prefab e no pause está ligado);
- `AudioDevicePanel` **sempre ligado** (no prefab começa desligado) e o `QuitButton` dele desligado.

Ou seja: no menu o painel de dispositivos aparece embutido, mas **nada chama
`VivoxManager.EnterTestVoiceChannel()`** — quem faz isso é `OptionsMenuManager.OpenAudioDevicePanel`
(`Assets/Scripts/Ui/OptionsMenuManager.cs:230-234`), ligado ao botão que foi escondido. Sem o canal
de eco o medidor fica parado e você não se ouve.

Correções possíveis:
- **A (só cena):** na instância do OptionsMenu do MainMenu, reverter só o override de “ativo” de
  `AudioDeviceButton`, `AudioDevicePanel` e `AudioDevicePanel/QuitButton` (no Inspector, botão
  direito no checkbox do GameObject → Revert). Fica igual ao pause; posição e tamanho do layout do
  menu continuam como estão.
- **B (manter o painel embutido):** botão “Testar microfone” que alterna
  `EnterTestVoiceChannel` / `LeaveTestVoiceChannel`.
- Nos dois casos, `CloseOptionsMenu` (`OptionsMenuManager.cs:203-206`) só desativa o objeto; deveria
  chamar `CloseSubPanels()` antes, senão fechar o Options com o teste aberto deixa o jogador preso no
  canal de eco.

### 3.3 Pause → testar microfone → sair: “perde a referência e buga tudo”

**Causa confirmada no código do SDK do Vivox 16.11.** Para o teste, `VivoxManager.ReconcileChannelsAsync`
(`Assets/Scripts/Network/VivoxManager.cs:233-269`) **sai do canal 3D da partida** e entra no eco; ao
fechar, sai do eco e entra de novo na partida. Quando é **você** quem sai de um canal, o SDK limpa os
participantes sem disparar `ParticipantRemovedFromChannel`
(`Library/PackageCache/com.unity.services.vivox@*/Runtime/VivoxServiceInternal.cs`, `OnChannelPropertyChanged`
→ “Cleaning up residual participants”, e `OnParticipantRemoved` ignora canal que já saiu). Ele
também destrói os taps.

Consequência no seu computador, depois de fechar o teste:
- `RemoteVoiceFilter` continua com o `_participant` antigo e, quando o Vivox avisa que os jogadores
  “entraram” de novo, sai na linha `if (_participant != null) return;` (`RemoteVoiceFilter.cs:73`).
  **Nenhum tap novo é criado** → as vozes passam a tocar pelo mix nativo do Vivox (fade Exponential
  do VivoxManager, sem paredes, sem reverb, fora do mixer do Unity) até o outro jogador ser recriado.
- `PlayerListInGameUi` mantém as linhas antigas; `PlayerVoiceIndicator` e `PlayerMicReporter` se
  recuperam (pegam a NullReferenceException e procuram de novo).

No computador dos outros: você **sai de verdade** do canal enquanto testa — some da lista e da voz
deles, e volta ao fechar.

Correções possíveis (da menor para a melhor):
1. Em `RemoteVoiceFilter.VivoxManager_OnParticipantJoined`, trocar o `return` por “se o participante
   guardado é outro objeto, `DestroyTap()` e refaz com o novo”.
2. No `VivoxManager`, assinar `VivoxService.Instance.ChannelLeft` e avisar quem guarda participantes
   (evento novo `OnLeftChannel`, ou disparar `OnParticipantLeftChannel` para cada um que estava lá).
3. **Não sair da partida para testar:** entrar no eco **junto** com o canal 3D e só trocar a
   transmissão (`SetChannelTransmissionModeAsync(TransmissionMode.Single, "MicTestChannel")`, e de
   volta para o canal da partida ao fechar). Ninguém sai da lista, você continua ouvindo o time e o
   time deixa de te ouvir só enquanto testa.

### 3.4 “Saio do canal quando me afasto” (sumir da lista do pause)

- O Vivox **tira da lista quem passa do Audible Distance do canal** (documentação do
  `Channel3DProperties.AudibleDistance` no próprio pacote). Antes de 2026-10-03 o canal usava 32 m —
  aí sim a pessoa sumia ao se afastar, e com taps havia um bug do servidor do Vivox em que ela não
  voltava (corrigido pela Unity em 30/01/2025).
- Desde o commit `8e28fc7f` (2026-10-03) o canal entra com `channelAudibleDistance = 1000`
  (`VivoxManager.cs:40-45` e `:277`), e a voz é zerada pelo RemoteVoiceFilter em ≤ 32 m. **Pelo código
  atual, distância não tira ninguém da lista.**
- O que ainda faz alguém sumir: **o teste de microfone** (3.3) — quem testa sai do canal — e um build
  com Channel Audible Distance diferente (o valor vai no nome do canal, aí os dois nem se ouvem).
- Recomendação: a lista do pause (`Assets/Scripts/Ui/PlayerListInGameUi.cs:99-120`) deveria vir dos
  **jogadores da partida** (Netcode / `PlayerVoiceIdentity`) e usar o Vivox só para o estado (falando
  / sem voz). Assim nada que dependa de distância vira informação de “quem está perto”.

### 3.5 TTS das doações muito alto

São dois caminhos, e só um passa pelo Unity:

- **Quem recebe a doação** ouve pelo *local playback* do TTS
  (`TextToSpeechMessageType.QueuedRemoteTransmissionWithLocalPlayback`,
  `Assets/Scripts/Audio/DonationTTSManager.cs:152`). Isso vai **direto para o dispositivo de saída do
  Vivox**, fora de qualquer AudioMixer. Nenhum slider do Options pega isso, só o “Volume de saída”
  do painel de teste de mic (`SetOutputDeviceVolume`, que não é salvo e também mexe no lobby e no eco).
- **Os outros** ouvem o TTS dentro da voz do destinatário: tap → VoiceGain → `VoiceSoundMixer/Voice`.
  O TTS entra no stream em nível cheio (não no nível do mic) e, durante a leitura, a voz vai ao
  alcance do grito (32 m) pelo `DonationReading`. Não existe slider de voz e o mixer não tem
  compressor/limiter.
- Extra: a voz configurada é `en_US female` lendo português.

Correções possíveis:
1. Slider **“Voz”** no Options ligado ao `voiceVolume` (já exposto no VoiceSoundMixer) e um
   **Compressor/Limiter** no grupo Voice — controla vozes e TTS dos outros.
2. Ganho próprio da leitura: em `RemoteVoiceFilter.ApplyRange`, multiplicar o ganho por um
   “Donation Reading Gain” (ex.: 0,5 = −6 dB) enquanto `IsReadingDonation()`.
3. Para quem recebe: um slider **“Doações (TTS)”** salvo em PlayerPrefs e aplicado com
   `SetOutputDeviceVolume` ao entrar no canal da partida (na partida todas as vozes vão por tap, então
   a saída do Vivox só carrega o TTS e o eco) e restaurado no lobby.
4. Trocar a voz para uma em português, se o Vivox tiver (a lista sai no login:
   `VivoxService.Instance.TextToSpeechAvailableVoices`).

---

## 4. Laboratório de voz no GYM

Uma réplica **só de geometria e som** do `Game.unity` (paredes, portas, colisores nas mesmas
camadas, luzes, geometria do Steam Audio, zonas de reverb), deslocada 250 m em X para não encostar no
GYM, com um **companheiro** que fala pela mesma cadeia de um jogador remoto.

- **Gerar / atualizar** (rode de novo sempre que o mapa mudar):
  `Tools/TCC/Level Design/Laboratório de voz/Gerar laboratório (réplica do Game)`
- **Abrir:** `Tools/TCC/Level Design/Laboratório de voz/Abrir GYM + laboratório de voz` → Play.
- **Desligue o “Mute Audio” da Game view**, senão não sai som (e o medidor do ouvido marca silêncio).

| Tecla | Faz |
|---|---|
| N / Shift+N | próximo / anterior cenário (te leva do GYM para a réplica) |
| P | põe o companheiro onde você está |
| H | companheiro de frente / de costas / parado |
| Y | voz liga/desliga |
| U | intensidade: sussurro → fala → grito → leitura de doação → sua gravação |
| I | **Atual (igual ao jogo) ↔ Proposta** |
| O / Shift+O | abre/fecha a porta mais perto / todas |
| J | silencia todo o resto (para medir) |
| K / Shift+K | grava 6 s do seu microfone e o companheiro fala com a sua voz / volta à fala gerada |
| L | anéis de alcance (amarelo = agora; azul/verde/vermelho = sussurro/fala/grito) |
| G | esconde o painel |

Cenários: 1 mesma sala → porta → corredor · 2 corredor reto · 3 virando o corredor · 4 sala vizinha
(através da parede) · 5 auditório (palco → plateia).

O painel mostra, em dB, quanto cada etapa tira da voz naquele instante — curva de distância, borda do
alcance, paredes/portas (oclusão e transmissão que o Steam Audio calculou de verdade), direção da boca,
ar, nível da fala, mixer — e o total.

**É fiel:** a cadeia do RemoteVoiceFilter, lida dos assets reais (Player.prefab, VoiceProfile,
VoiceSpatialProfile, VoiceSoundMixer, Audible Distance do HostManager): mexa neles em Play e o
companheiro muda em até 1 s. Geometria, camadas, materiais e zonas são cópia do Game.
**Não reproduz:** a rede do Vivox (lista, canais, teste de mic, local playback do TTS) e a energia
real do microfone, que é simulada a partir do áudio (5×/s, como o Vivox). Para calibrar, compare
com o medidor do Options → Testar microfone.

Arquivos: `Assets/GYM/VoiceLab/Scripts/` (GymVoiceLab, GymVoiceLabSpeaker, GymVoiceLabDoor),
`Assets/GYM/VoiceLab/Audio/` (falas pt-BR geradas com a voz Maria do Windows),
`Assets/GYM/Editor/VoiceLab/GymVoiceLabBuilder.cs`, `Assets/GYM/Scenes/GYM_VozLab.unity` (~11,7 MB,
pode ser regerada a qualquer momento).

---

## 5. Medições do laboratório

Feitas em Play no laboratório (2026-11-01), com o companheiro de frente para você e o resto do áudio
silenciado. Os números são o **nível relativo ao que a voz teria a 1 m** (0 dB), somando o que o
Steam Audio, a curva do AudioSource e a janela aplicaram de fato. “Mudo” = janela fechada (a voz não
sai). Cada ponto: 2,4 s de amostra, mediana.

Validação: na Proposta, a 9,5 m no corredor, o painel previu −10,3 dB sobre a fonte (−19,4 dBFS),
ou seja −29,7 dBFS; o `AudioListener` mediu −30,4 dBFS.

### 5.1 Corredor reto, sem nada no caminho — fala normal

| Distância | Atual: nível | Atual: alcance (mín–máx) | Atual: tempo mudo | Proposta: nível | Proposta: tempo mudo |
|---|---|---|---|---|---|
| 1,5 m | −4 dB | 8–20 m | 0% | 0 dB | 0% |
| 3,5 m | −15 dB | 8–21 m | 0% | −4 dB | 0% |
| 7,5 m | −54 dB (abre só nas sílabas fortes) | 8–23 m | 0% | −9 dB | 0% |
| 11,5 m | −38 dB | 8–23 m | **39%** | −12 dB | 0% |
| 15,5 m | −52 dB | 8–21 m | **44%** | −14 dB | 0% |
| 19,5 m | mudo | 8–23 m | **76%** | −17 dB | 0% |
| 23,5 m | mudo | 13–22 m | **100%** | −19 dB | 0% |
| 27,5 m | mudo | 8–23 m | **100%** | −29 dB | 14% |

Grito, Atual: mudo 26% do tempo a 15,5 m, 44% a 19,5 m e ~70% a 23–27 m (o alcance desaba entre
as palavras). Proposta: −8 dB a 15,5 m, −10 dB a 23,5 m, −15 dB a 27,5 m.

### 5.2 Leitura de doação (TTS) x fala, Atual

| Distância | Fala normal | Leitura de doação |
|---|---|---|
| 11,5 m | −38 dB, 39% mudo | −24 dB, nunca muda |
| 15,5 m | −52 dB, 44% mudo | −29 dB |
| 19,5 m | 76% mudo | −34 dB |
| 23,5 m | mudo | −41 dB |

A leitura fica com o alcance do grito (32 m) parado, sem a oscilação da fala: chega mais longe e
mais firme do que qualquer voz. Somado ao TTS entrar no stream em nível cheio, é por isso que ele
parece alto para os outros. Para quem recebe, o volume nem passa pelo Unity (3.5).

### 5.3 Situações

| Situação | Atual | Proposta |
|---|---|---|
| De costas para você (8 m) | diretividade −8,0 dB | −3,1 dB |
| Sair da sala pela **porta aberta** (companheiro no meio da sala) | −10 (2,5 m) → −17 (na porta) → −22 (8 m, oclusão parcial) → **−58 (12 m: parede −25 dB + 44% mudo)** | −1 → −7 → −9 → **−25 (12 m: parede −13 dB)** → −27 (16 m) |
| Sair da sala com a **porta fechada** | porta −20 dB → −43 a −60, mudo a 10 m | −24 a −35, nunca mudo até 23 m |
| Virar o corredor (companheiro a ~9 m) | −24 antes da curva (já 29% mudo) → **−50 a −56 logo depois** → 64% mudo a 15 m | −10 → **−27** (degrau de 17 dB) → −30 a 15 m |
| Salas vizinhas, portas fechadas (4 / 8,5 / 12,5 m) | −40 / mudo / −60 | −21 / −26 / −29 |
| Salas vizinhas, portas abertas | −40 / mudo / mudo | −15 / −26 / −25 |
| Auditório, palco → plateia (3,4 / 6,8 / 11 m) | −12 / −24 / −75 | −4 / −8 / −11 |

Na Proposta ainda existe um degrau ao perder a linha de visão (a parede vira −16 dB de uma vez): o
som continua, mas a transição não é rampa. Os botões para suavizar mais são o piso de transmissão
(subir de 0,15 para ~0,25 ≈ −12 dB) e o Source Radius (até 2 m). Contornar a quina de verdade
(*pathing*) não é possível com a cena do Steam Audio em física do Unity.

**Sobre a energia simulada:** o quanto o alcance Atual oscila depende do medidor de energia real do
Vivox. No laboratório ele vem do próprio áudio, 5×/s, com o pico de cada intensidade configurável
no companheiro (Fala = 0,5). Curva, janela, paredes e diretividade são exatos. Para ver com a sua
voz, grave com K e compare com o medidor do Options → Testar microfone.

---

## 6. Outros achados no caminho

- **Reverb do auditório quase nunca liga:** o `AuditoriumVoiceZoneTrigger` do Game.unity tem um
  BoxCollider de **1 × 2 × 1 m** em (11,1; 2,5; 31,4). Quem fala no palco fora desse cubo não ganha
  o reverb “Auditorium”.
- **Zona “Psychotic” no lado esquerdo do mapa:** o objeto `VoiceZoneTrigger` do Game.unity
  (centro −24,8; 0; 17,6, caixa de 50 × 10 × 20 m) usa o preset padrão do prefab
  `Assets/Prefabs/Sound/VoiceZoneTrigger.prefab`, que é **Psychotic** desde o commit `17fdef70`
  (2026-08-16, “Presets working”, antes era Underwater e Room). Toda voz de quem está ali sai com esse
  efeito. Conferir se é de propósito.
- **Prefabs apagados ainda usados no Game.unity:** 95 instâncias (`Floor_Hallway`, `Hallway_05`,
  `Floor_Hallway 1`, `Floor_Hallway 2`) apontam para prefabs removidos no commit `2bb157e4`
  (2026-10-27). Elas não renderizam nem colidem; o Unity reclama toda vez que a cena abre.
- **`OptionsMenuManager.CloseOptionsMenu`** não fecha o teste de mic (ver 3.2).
- **`AudioDeviceSettings`** (amostra do Vivox modificada) não remove os eventos
  `EffectiveInputDeviceChanged/EffectiveOutputDeviceChanged` no `OnDestroy`; ao sair da partida o
  painel do pause é destruído e o Vivox pode chamar um objeto morto quando o dispositivo muda.
