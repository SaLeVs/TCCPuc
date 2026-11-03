# Voz (Vivox) e som — como funciona, onde ajustar e o que mudou

**Atualizado em:** 2026-11-02 · **Branch:** `VivoxCorrections`
(código no commit `8e478131`; assets, prefabs, Options, mixer e zona do auditório depois dele;
ficha de passagem de som em 2026-11-02, seção 3.7)

Este documento tem três partes: como a voz anda hoje e onde fica cada ajuste (seções 1–2), o que
estava errado e como foi corrigido (3), e o laboratório de voz no GYM com as medições de antes e
depois (4–5).

---

## 1. Como a voz anda

```
Microfone do jogador A
  └─ volume de entrada salvo para esse microfone   MicSettings (medido na ficha de passagem de som)
  └─ Vivox (login + canal)                         VivoxManager  (HostManager.prefab, cena Netbootstrap)
       ├─ lobby  : canal 2D  "<lobbyId>_lobby"     TextAndAudio, sem posição
       ├─ partida: canal 3D  "<lobbyId>_game"      AudioOnly, posição enviada por VivoxPlayer.Update
       └─ teste  : canal de eco "MicTestChannel"   JUNTO com o canal atual; o mic só transmite para o eco

No computador do jogador B, para cada jogador remoto (RemoteVoiceFilter no Player.prefab):
  tap do participante (silenciado no mix do Vivox)  → AudioSource 3D no VoiceAnchor (boca)
    → Steam Audio (HRTF, curva de distância do perfil de voz, ar, oclusão/transmissão com piso, diretividade)
    → VoiceGain (volume que B escolheu no pause; × volume das doações enquanto A "lê" uma doação)
    → AudioReverbFilter (reverb da zona onde A está)
    → mixer VoiceSoundMixer / Voice (Compressor) → Master (voiceVolume = slider "Vozes")  → saída

Volume do AudioSource = "janela" que fecha a voz só no fim do alcance. O alcance fica no da fala
(24 m) enquanto A fala e 1,5 s depois; sobe até o do grito (40 m) com a voz mais alta; volta ao do
sussurro (8 m) quando A para de falar.

Outros caminhos:
  monstro ouvindo  : PlayerMicReporter (energia do próprio mic → NoiseBus, 10 m fala / 24 m grito; nada durante o teste de mic)
  lista do pause   : PlayerListInGameUi  → jogadores da PARTIDA (Netcode); Vivox só diz quem está "(sem voz)"
  ícone de voz     : PlayerVoiceIndicator → VivoxParticipant.SpeechDetected
  TTS das doações  : DonationTTSManager → TTS do Vivox pela voz do destinatário
                     · o destinatário ouve pela saída própria do Vivox (VivoxManager aplica o volume das doações)
                     · os outros ouvem pela voz dele (RemoteVoiceFilter aplica o volume de cada um)
```

---

## 2. Onde ajustar cada coisa

| O quê | Onde | Valor | O que muda |
|---|---|---|---|
| Alcance do grito (e da leitura de doação) | `Assets/Prefabs/Network/HostManager.prefab` → VivoxManager → **Audible Distance** | **40** (era 32) | Onde a voz chega a zero no grito. |
| Alcance do canal Vivox (lista) | VivoxManager → **Channel Audible Distance** | 1000 | Vai no nome do canal (`!p-1000-…`): tem que ser igual em todo build. |
| Fade do próprio Vivox | VivoxManager → Conversational Distance / Fade Intensity / Fade Model | 3 / 1 / Exponential | Só vale no lobby e no eco (o que não tem tap). |
| Limiares sussurro / fala / grito | `Assets/Scriptable_objects/Sounds/VoiceProfile.asset` | fala 0,25 · grito 0,65 | Energia do Vivox (0..1). Calibre com o medidor do teste de mic. |
| Alcance do sussurro / da fala | VoiceProfile → **Whisper Range / Speech Range** | 8 m / **24 m** (novo) | Quanto a voz alcança calada/falando. |
| Lei de distância | VoiceProfile → **Near Field / Decibels Per Doubling** | **2 m / 4,5 dB** (era 1 m / 6 dB) | Quanto a voz cai com a distância. |
| Borda do alcance | VoiceProfile → **Edge Fade Start** | **0,6** (novo) | A voz só começa a fechar nos últimos 40% do alcance. |
| Envelope do alcance | `Player.prefab` → RemoteVoiceFilter → Attack / **Release** / **Hold** | 0,08 s / **2,5 s** / **1,5 s** | Release menor = a voz encolhe mais rápido nas pausas. |
| Espacialização da voz | `Assets/Scriptable_objects/Sounds/Spatial/VoiceSpatialProfile.asset` | Source Radius **1,0** · Directivity **0,15** · **Minimum Transmission 0,22 / 0,15 / 0,05** | Vazamento pelo vão da porta, perda de costas (−3 dB), mínimo que passa por paredes (≈ −16 dB, abafado). |
| Paredes e portas (todos os sons) | `Assets/Scriptable_objects/Sounds/Acoustics/Wall.asset` / `Door.asset` | 0,056 / 0,1 (médios) | Não mexido: vale para o mundo inteiro. O piso acima é só da voz. |
| Camadas que bloqueiam som | `Assets/Plugins/SteamAudio/Resources/SteamAudioSettings.asset` → Layer Mask | Default, Walls, Doors | — |
| Volume de todas as vozes | Options → **Vozes** → `VoiceSoundMixer` `voiceVolume` | 100% | Novo slider. Salvo em `VoiceVolume`. |
| Compressor das vozes | `Assets/Sounds/VoiceSoundMixer.mixer` → grupo Voice → **Compressor** | −16 dB, 10 ms, 200 ms | Segura picos (grito perto, TTS) sem mexer em vozes distantes. Ajuste na janela Audio Mixer. |
| Volume de cada jogador | Pause → lista → slider | 0 (−50..50) | Igual antes. |
| Volume do **seu** microfone | Ficha de passagem de som (cena `MicSetup`) ou Opções → Dispositivos → slider embaixo do dispositivo de entrada | medido (−50..+25 na ficha) | O quanto todos te ouvem e onde caem seu sussurro, fala e grito. Salvo por microfone em `MicInputVolume_<nome do microfone>`. |
| Volume das doações (TTS) | Options → **Doações (TTS)** → `VoiceChatSettings` | **50%** | Para quem recebe e para quem ouve os outros. Salvo em `DonationTtsVolume`. |
| Ouvir as doações | Options → **Ler doações: Sim/Não** | Sim | Desligado: você não ouve nenhuma leitura (a sua continua indo para os outros). |
| Voz do TTS | `DonationAudioManager.prefab` → DonationTTSManager → Preferred Voice Prefixes / Tts Voice | `pt_BR`, `pt` / `en_US female` | Usa uma voz em português se o Vivox tiver; a lista sai no log ao logar. |
| Reverb do auditório | `Game.unity` → `AuditoriumVoiceZoneTrigger` | caixa 32 × 6,5 × 41 m | Cobre plateia e palco (era um cubo de 1 m). |
| Preset padrão das zonas | `Assets/Prefabs/Sound/VoiceZoneTrigger.prefab` | **Off** (era Psychotic) | A zona grande do lado esquerdo do mapa fica sem efeito até alguém escolher um preset. |
| Monstro ouvindo a voz | Player.prefab → PlayerMicReporter | 10 m / 24 m / 0,35 s | Igual antes. |

---

## 3. O que estava errado e como ficou

### 3.1 Volume x distância — **corrigido**
Antes, a fala normal sumia por volta de 10–12 m mesmo num corredor reto, e caía ~25 dB de uma vez
quando uma parede entrava no caminho. Cinco perdas somadas: alcance seguindo cada sílaba (8–22 m),
janela (1 − x²)² no alcance inteiro, 1/d desde 1 m, parede ≈ −25 dB sem contorno, −8 dB de costas.
Agora: lei de 4,5 dB/dobra desde 2 m, alcance da fala fixo em 24 m com hold de 1,5 s, borda só nos
últimos 40%, piso de transmissão para a voz, fonte de 1 m e diretividade 0,15
(`RemoteVoiceFilter.RangeFor` / `ApplyRange`, `VoiceProfileSO.BuildRolloff`,
`SteamAudioTransmissionFloor`). Medições na seção 5.

### 3.2 Menu principal sem botão de testar o microfone — **corrigido (opção B)**
O painel de dispositivos continua embutido no Options do menu; o botão **TESTAR MICROFONE** embaixo
dele alterna o teste (`OptionsMenuManager.ToggleMicTest`) e vira **PARAR TESTE** enquanto roda.
Fechar o Options (ou sair da cena) sempre encerra o teste (`OnDisable`).

### 3.3 Pause → testar microfone → sair "buga tudo" — **corrigido**
Causa: o teste saía do canal da partida, e quando é você quem sai o Vivox limpa os participantes sem
avisar; na volta o `RemoteVoiceFilter` recusava os participantes novos e as vozes ficavam sem tap.
Agora o teste entra no eco **junto** com o canal da partida e só a transmissão vai para o eco
(`VivoxManager.JoinTestChannelAsync` / `LeaveTestChannelAsync`): você continua ouvindo o time,
ninguém te ouve testando e ninguém some da lista. Por segurança, o `RemoteVoiceFilter` também
refaz o tap quando recebe um participante novo, e o `VivoxManager.OnLeftChannel` avisa quem guarda
participantes quando um canal é deixado.

### 3.4 Sumir da lista do pause — **corrigido**
A lista agora vem dos jogadores da partida (`PlayerInfos`), com "(sem voz)" para quem não está no
canal. Distância nunca tira ninguém da lista.

### 3.5 TTS das doações muito alto — **corrigido**
Slider **Doações (TTS)** (padrão 50% ≈ −6 dB) e **Ler doações: Sim/Não** no Options, Compressor no
grupo Voice e slider **Vozes** geral. Desligado, quem recebe não toca o TTS localmente
(`QueuedRemoteTransmission`), e quem ouve zera a voz do destinatário durante a leitura.

### 3.6 Ícone de voz não acendia e alcance preso no sussurro — **corrigido**
Medido numa partida real (host + jogador virtual do MPPM, 2026-11-01): para um jogador **com tap**,
o Vivox deixa `AudioEnergy` em 0 e `SpeechDetected` em falso o tempo todo, mesmo com a voz tocando
(pico 0,71 no tap). Para o próprio jogador, `SpeechDetected` fica sempre verdadeiro, até em silêncio.
Consequências: o ícone do nome nunca acendia e o `RemoteVoiceFilter` achava que ninguém falava, então
o alcance de toda voz remota ficava no do sussurro (8 m) — provavelmente a raiz do "saio da sala e
não ouço" desde o começo.
Agora o `RemoteVoiceFilter.HeardEnergy` mede a voz no próprio tap (50 ms logo à frente da reprodução,
antes de distância e paredes) e converte para a escala do Vivox com a calibração medida
(`VoiceProfile` → Heard Silence Db −97 / Heard Full Db 21: energia 0,45 ≈ −44 dB, 0,65 ≈ −22 dB).
O valor vai para `VivoxManager.GetHeardEnergy`, que o ícone lê (acende a partir de 0,2).

### 3.7 Microfone alto demais desde a primeira palavra — **ficha de passagem de som** (2026-11-02)
Caso real: o Igor começou a falar e "destruiu" o Jorge. O controle automático de ganho do Vivox fica
desligado de propósito (`HostManager.prefab` → VivoxManager → Automatic Gain Control), senão sussurro,
fala e grito saem quase iguais. Sem ele, cada microfone saía com o volume que o hardware e o Windows
davam (o "Aumento do microfone" sozinho soma +10 a +30 dB), e nada calibrava isso: o slider de volume
de entrada do Options não era salvo. No lobby a voz nem passa pelo mixer do Unity, então ali não havia
proteção nenhuma. E os limiares do `VoiceProfile` são fixos: com o microfone quente, a fala normal
contava como grito (40 m de alcance, monstro ouvindo de 24 m); com um microfone baixo, o contrário.

Agora:
- **Cena `MicSetup`** (`Assets/Scenes/MicSetup.unity`, cópia da mesa do Bootstrap com uma "ficha de
  passagem de som" no papel do contrato). Aparece entre o Netbootstrap e o MainMenu **na primeira vez
  com cada microfone** (`ClientGameManager.StartMenu` → `MicSettings.NeedsSetup`).
- O jogador lê três frases: **fale normal**, **sussurre**, **grite**. A ficha mede a energia do próprio
  Vivox (a mesma que todos os limiares do jogo leem) no canal de eco, com a saída do Vivox e a música
  mudas, e ajusta o volume de entrada: a fala normal vai para o meio da faixa de fala (≈ 0,45) e,
  dentro de ±6 dB disso, o sussurro fica abaixo de 0,25 e o grito acima de 0,65. Quando o sussurro e o
  grito da pessoa ficam perto demais para os dois caberem, o sussurro tem prioridade (é a mecânica de
  se esconder do monstro).
- Ajuste de 10 dB ou mais (ou microfone estourando) repete as frases para conferir, até 3 rodadas.
- Depois vem o **teste livre**: o quadro "O QUE O JOGO ENTENDE" mostra sussurro/fala/grito ao vivo,
  dá para mexer ±2 dB e **ASSINAR E CONTINUAR** salva. **PULAR** não salva (a ficha volta na próxima vez).
- `MicSettings` guarda o valor por microfone; o `VivoxManager` aplica ao logar e ao trocar de
  microfone. O volume de entrada do Vivox vale para todos os canais: lobby do menu, lobby 3D e partida.
- O slider de entrada em Opções → Dispositivos agora salva o valor para o microfone em uso.

Arquivos: `Assets/Scripts/Network/MicSettings.cs`, `Assets/Scripts/Ui/MicSetupScreen.cs`,
`Assets/Scenes/MicSetup.unity`; ajustes pequenos em `VivoxManager`, `ClientGameManager.StartMenu`,
`Loader.Scene.MicSetup`, `AudioDeviceSettings` (+ referência `Network` no `ChatChannelSample.asmdef`).
Cada rodada sai no Console como `[MicSetup] Round N at X dB ...: whisper, talk, shout, floor -> Y dB`.

Para ver a ficha de novo num microfone já calibrado: apagar a chave `MicInputVolume_<nome>` do
PlayerPrefs (ou trocar de microfone).

Pendente: teste com voz de verdade (a conta de dB por energia vem da calibração da seção 3.6 e ainda
precisa ser conferida: a rodada 2 do log mostra se a energia acompanhou o ajuste); botão para refazer
a ficha no quadro de Opções do menu.

### Não testado com Vivox real
Tudo acima compila, foi conferido em Play no GYM (curva, paredes, reverb, Options do pause, lista) e
renderizado no menu. O que depende de uma sessão Vivox de verdade não foi rodado daqui, para não abrir
o microfone sem ninguém acompanhando:
- teste de mic com o eco ao lado do canal da partida (lista intacta, o time sem ouvir o teste);
- volume das doações de quem recebe (via `SetOutputDeviceVolume`, tratando as unidades do Vivox como dB);
- escolha automática da voz em português.

Roteiro rápido: menu → Opções → **TESTAR MICROFONE** (você deve se ouvir) → **PARAR TESTE**; numa
partida com duas pessoas, pausa → Opções → Microfone → fala → fecha: o outro não te ouve testando,
você continua ouvindo ele, e a voz continua abafando atrás das paredes depois de fechar.

---

## 4. Laboratório de voz no GYM

Réplica **só de geometria e som** do `Game.unity` (paredes, portas, colisores nas mesmas camadas,
luzes, geometria do Steam Audio, zonas de reverb), 250 m ao lado do GYM, com um **companheiro** que
fala pela mesma cadeia de um jogador remoto.

- **Gerar / atualizar** (de novo sempre que o mapa mudar):
  `Tools/TCC/Level Design/Laboratório de voz/Gerar laboratório (réplica do Game)`
- **Abrir:** `Tools/TCC/Level Design/Laboratório de voz/Abrir GYM + laboratório de voz` → Play.
- **Desligue o "Mute Audio" da Game view**, senão não sai som.

| Tecla | Faz |
|---|---|
| N / Shift+N | próximo / anterior cenário (te leva do GYM para a réplica) |
| P | põe o companheiro onde você está |
| H | companheiro de frente / de costas / parado |
| Y | voz liga/desliga |
| U | intensidade: sussurro → fala → grito → leitura de doação → sua gravação |
| I | **Atual (igual ao jogo) ↔ Antigo (até 01/11)** |
| O / Shift+O | abre/fecha a porta mais perto / todas |
| J | silencia todo o resto (para medir) |
| K / Shift+K | grava 6 s do seu microfone e o companheiro fala com a sua voz / volta à fala gerada |
| L | anéis de alcance (amarelo = agora; azul/verde/vermelho = sussurro/fala/grito) |
| G | esconde o painel |

"Atual" lê os assets do jogo a cada segundo: mexa no VoiceProfile, no VoiceSpatialProfile ou no
RemoteVoiceFilter do Player.prefab em Play e ouça na hora. "Antigo" é a voz de antes, fixa, para
comparar. A energia do microfone é simulada a partir do áudio (5×/s, como o Vivox).

Arquivos: `Assets/GYM/VoiceLab/` (scripts, falas pt-BR geradas com a voz Maria do Windows, material),
`Assets/GYM/Editor/VoiceLab/GymVoiceLabBuilder.cs`, `Assets/GYM/Scenes/GYM_VozLab.unity` (~12 MB,
gerada — cada geração reescreve o arquivo inteiro).

---

## 5. Medições (laboratório, 2026-11-01)

Nível relativo ao que a voz teria a 1 m (0 dB), mediana de 2,4 s em cada ponto, companheiro de frente
para você. "Mudo" = parte do tempo em que a janela fecha a voz.

### 5.1 Corredor reto, fala normal

| Distância | Antigo | Atual |
|---|---|---|
| 3,5 m | −15 dB | −4 dB |
| 7,5 m | −54 dB (abre só nas sílabas fortes) | −9 dB |
| 11,5 m | mudo 59% do tempo | −12 dB |
| 15,5 m | mudo 53% | −14 dB |
| 19,5 m | mudo 72% | −15 dB |
| 23,5 m | mudo 100% | −22 dB |
| 27,5 m | mudo 100% | fechando (mudo 49%) |

Grito: antes mudo 30–87% do tempo a partir de 19,5 m; agora −9 dB a 19,5 m e −12,5 dB a 27,5 m.

### 5.2 Situações

| Situação | Antigo | Atual |
|---|---|---|
| De costas para você (8 m) | −45 dB (diretividade −8 dB + borda) | −12 dB (diretividade −3 dB) |
| Sair da sala pela porta aberta (12 m) | mudo 71% (parede −25 dB) | −25 dB (parede −13 dB) |
| Virar o corredor (logo depois da quina) | −51 dB, mudo 27–46% | −27 dB, nunca mudo |
| Leitura de doação a 15,5 m | −29 dB (alcance 32 m, volume cheio) | −20 dB a 50% (6 dB abaixo de uma fala na mesma distância) |
| Companheiro no palco do auditório | reverb Off (zona de 1 m) | reverb Auditorium |

Ainda existe um degrau quando a parede entra na linha de visão (−16 dB do piso): a voz continua,
abafada. Para suavizar mais, suba o Minimum Transmission (ex.: 0,25 médio ≈ −12 dB) ou o Source Radius
(até 2 m) no VoiceSpatialProfile — e confira no laboratório.

---

## 6. Ainda em aberto

- **Prefabs apagados ainda usados no Game.unity:** 95 instâncias (`Floor_Hallway`, `Hallway_05`,
  `Floor_Hallway 1`, `Floor_Hallway 2`) apontam para prefabs removidos no commit `2bb157e4`
  (2026-10-27). Não renderizam nem colidem; o Unity reclama toda vez que a cena abre. É do level design.
- **Zona `VoiceZoneTrigger` do lado esquerdo do mapa** (50 × 10 × 20 m): agora sem efeito (preset Off).
  Escolher um preset para ela ou apagá-la.
