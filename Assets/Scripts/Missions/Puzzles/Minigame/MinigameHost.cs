using System;
using Enums;
using Interfaces;
using Player;
using UnityEngine;

namespace Missions.Puzzles
{
    // Fica no Player. Abre o prefab do minigame dentro do container, trava input e cursor,
    // e fecha sozinho se o jogador for derrubado ou morrer.
    public class MinigameHost : MonoBehaviour
    {
        [Tooltip("Onde os minigames são instanciados. Vazio = no próprio objeto deste componente.")]
        [SerializeField] private RectTransform container;

        public bool IsOpen => _current != null;

        private PlayerState _playerState;
        private PlayerKnockdown _playerKnockdown;

        private MinigameBase _current;
        private Action _onSucceeded;
        private Action _onFailed;


        private void Awake()
        {
            _playerState = GetComponentInParent<PlayerState>(true);
            _playerKnockdown = GetComponentInParent<PlayerKnockdown>(true);

            if (container == null)
            {
                container = (RectTransform)transform;
            }
        }

        private void OnEnable()
        {
            if (_playerState != null) _playerState.OnPlayerDead += PlayerState_OnPlayerDead;
            if (_playerKnockdown != null) _playerKnockdown.OnKnockdownChanged += PlayerKnockdown_OnKnockdownChanged;
        }

        public bool Open(MinigameBase minigamePrefab, Action onSucceeded, Action onFailed = null)
        {
            if (IsOpen || minigamePrefab == null || _playerState == null) return false;
            if (_playerState.IsDead) return false;
            if (_playerKnockdown != null && _playerKnockdown.IsKnockedDown) return false;

            _onSucceeded = onSucceeded;
            _onFailed = onFailed;

            _current = Instantiate(minigamePrefab, container);
            _current.gameObject.SetActive(true);

            _current.OnSucceeded += Minigame_OnSucceeded;
            _current.OnFailed += Minigame_OnFailed;
            _current.OnCancelled += Close;

            _playerState.SetInputLocked(InputLockReason.Minigame, true);
            CursorState.Hold(CursorReason.MissionUi);

            _current.Begin();
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;

            _current.OnSucceeded -= Minigame_OnSucceeded;
            _current.OnFailed -= Minigame_OnFailed;
            _current.OnCancelled -= Close;

            _current.Stop();
            Destroy(_current.gameObject);

            _current = null;
            _onSucceeded = null;
            _onFailed = null;

            if (_playerState != null)
            {
                _playerState.SetInputLocked(InputLockReason.Minigame, false);
            }

            CursorState.Release(CursorReason.MissionUi);
        }

        private void Minigame_OnSucceeded()
        {
            Action onSucceeded = _onSucceeded;

            Close();
            onSucceeded?.Invoke();
        }

        private void Minigame_OnFailed() => _onFailed?.Invoke();

        private void PlayerState_OnPlayerDead(bool isDead)
        {
            if (isDead) Close();
        }

        private void PlayerKnockdown_OnKnockdownChanged(bool isKnockedDown)
        {
            if (isKnockedDown) Close();
        }

        private void OnDisable()
        {
            Close();

            if (_playerState != null) _playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
            if (_playerKnockdown != null) _playerKnockdown.OnKnockdownChanged -= PlayerKnockdown_OnKnockdownChanged;
        }

    }
}
