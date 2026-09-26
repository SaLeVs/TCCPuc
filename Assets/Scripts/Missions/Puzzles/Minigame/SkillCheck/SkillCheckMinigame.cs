using UnityEngine;

namespace Missions.Puzzles
{
    public class SkillCheckMinigame : MinigameBase
    {
        [SerializeField] private RectTransform checkArea;
        [SerializeField] private CircularPointer pointer;
        [SerializeField] private SkillCheckGenerator generator;

        [Tooltip("Quantos graus o ponteiro pode estar longe do centro da área para contar como acerto.")]
        [SerializeField, Range(1f, 45f)] private float toleranceDegrees = 20f;
        [SerializeField, Min(1)] private int requiredCorrectChecks = 3;
        [Tooltip("Errar zera o progresso e sorteia as áreas de novo (evita ganhar spammando clique).")]
        [SerializeField] private bool resetProgressOnMiss = true;

        // Cada acerto gasta uma área: pedir mais acertos que áreas travaria o minigame.
        private int RequiredChecks => Mathf.Min(requiredCorrectChecks, generator.SlotCount);

        private int _currentCorrectChecks;
        private bool _isRunning;


        private void Awake()
        {
            if (pointer == null)
            {
                pointer = GetComponentInChildren<CircularPointer>(true);
            }
        }

        public override void Begin() => Restart();

        public override void Stop() => _isRunning = false;

        // Ligado no OnClick do botão "Check".
        public void Check()
        {
            if (!_isRunning || generator.CurrentSlot == null) return;

            if (!IsPointerOnCurrentSlot())
            {
                Fail();

                if (resetProgressOnMiss)
                {
                    Restart();
                }

                return;
            }

            _currentCorrectChecks++;

            if (_currentCorrectChecks >= RequiredChecks)
            {
                _isRunning = false;
                Succeed();
                return;
            }

            generator.GenerateNewSlot();
        }

        private void Restart()
        {
            _currentCorrectChecks = 0;
            generator.Regenerate();
            generator.GenerateNewSlot();
            _isRunning = true;
        }

        // Compara ângulos em volta do pivô do ponteiro: não depende da resolução nem da escala do Canvas.
        private bool IsPointerOnCurrentSlot()
        {
            Vector2 center = pointer.PointerRect.position;

            float pointerAngle = AngleAround(center, checkArea.position);
            float slotAngle = AngleAround(center, generator.CurrentSlot.Rect.position);

            return Mathf.Abs(Mathf.DeltaAngle(pointerAngle, slotAngle)) <= toleranceDegrees;
        }

        private static float AngleAround(Vector2 center, Vector2 point)
        {
            Vector2 direction = point - center;
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private void OnValidate()
        {
            if (generator != null && requiredCorrectChecks > generator.SlotCount)
            {
                requiredCorrectChecks = generator.SlotCount;
            }
        }

    }
}
