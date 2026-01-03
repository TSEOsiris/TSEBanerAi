using System;
using System.Drawing;

namespace TSEBanerAi.Dice
{
    /// <summary>
    /// Handles D20 dice animation state for UI rendering
    /// </summary>
    public class DiceAnimator
    {
        private bool _isAnimating;
        private float _animationTime;
        private float _animationDuration = 1.5f; // Total animation time in seconds
        private int _displayNumber;
        private int _finalResult;
        private DiceRollResult _rollResult;
        private DiceAnimationPhase _phase;

        /// <summary>
        /// Whether animation is currently playing
        /// </summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Current number to display on dice
        /// </summary>
        public int DisplayNumber => _displayNumber;

        /// <summary>
        /// Current animation phase
        /// </summary>
        public DiceAnimationPhase Phase => _phase;

        /// <summary>
        /// Final roll result (available after animation)
        /// </summary>
        public DiceRollResult RollResult => _rollResult;

        /// <summary>
        /// Animation progress (0-1)
        /// </summary>
        public float Progress => _isAnimating ? Math.Min(1f, _animationTime / _animationDuration) : 1f;

        /// <summary>
        /// Event fired when animation completes
        /// </summary>
        public event Action<DiceRollResult> OnAnimationComplete;

        /// <summary>
        /// Start dice animation for a roll
        /// </summary>
        public void StartAnimation(DiceRollResult result)
        {
            _rollResult = result;
            _finalResult = result.BaseRoll;
            _animationTime = 0f;
            _displayNumber = 10;
            _phase = DiceAnimationPhase.Rolling;
            _isAnimating = true;
        }

        /// <summary>
        /// Update animation state
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isAnimating) return;

            _animationTime += deltaTime;
            float progress = _animationTime / _animationDuration;

            if (progress < 0.7f)
            {
                // Rolling phase - rapidly changing numbers
                _phase = DiceAnimationPhase.Rolling;
                _displayNumber = (int)((_animationTime * 20) % 20) + 1;
            }
            else if (progress < 0.85f)
            {
                // Slowing down phase
                _phase = DiceAnimationPhase.SlowingDown;
                float slowProgress = (progress - 0.7f) / 0.15f;
                int interval = (int)(slowProgress * 10) + 1;
                if ((int)(_animationTime * 10) % interval == 0)
                {
                    _displayNumber = new Random().Next(1, 21);
                }
            }
            else if (progress < 1.0f)
            {
                // Settling phase - show final result
                _phase = DiceAnimationPhase.Settling;
                _displayNumber = _finalResult;
            }
            else
            {
                // Animation complete
                _phase = DiceAnimationPhase.Complete;
                _displayNumber = _finalResult;
                _isAnimating = false;
                OnAnimationComplete?.Invoke(_rollResult);
            }
        }

        /// <summary>
        /// Get color based on current state
        /// </summary>
        public Color GetDiceColor()
        {
            if (_rollResult == null) return Color.FromArgb(255, 60, 60, 80);

            if (_phase == DiceAnimationPhase.Complete || _phase == DiceAnimationPhase.Settling)
            {
                if (_rollResult.IsCriticalSuccess)
                    return Color.FromArgb(255, 255, 215, 0); // Gold
                if (_rollResult.IsCriticalFailure)
                    return Color.FromArgb(255, 139, 0, 0); // Dark red
                if (_rollResult.IsSuccess)
                    return Color.FromArgb(255, 34, 139, 34); // Green
                return Color.FromArgb(255, 178, 34, 34); // Red
            }

            // Rolling color - white/gray
            return Color.FromArgb(255, 200, 200, 200);
        }

        /// <summary>
        /// Get scale factor for animation
        /// </summary>
        public float GetScale()
        {
            if (!_isAnimating) return 1.0f;

            float progress = Progress;
            
            // Bounce effect at the end
            if (progress > 0.85f)
            {
                float bounceProgress = (progress - 0.85f) / 0.15f;
                return 1.0f + (float)Math.Sin(bounceProgress * Math.PI) * 0.3f;
            }

            // Slight pulsing during roll
            return 1.0f + (float)Math.Sin(_animationTime * 10) * 0.1f;
        }

        /// <summary>
        /// Get rotation angle in degrees
        /// </summary>
        public float GetRotation()
        {
            if (!_isAnimating || _phase == DiceAnimationPhase.Complete) return 0f;

            float progress = Progress;
            
            // Fast rotation during rolling, slowing down at end
            float rotationSpeed = (1.0f - progress) * 720f; // 720 degrees per second at start
            return _animationTime * rotationSpeed;
        }

        /// <summary>
        /// Cancel current animation
        /// </summary>
        public void Cancel()
        {
            _isAnimating = false;
            _phase = DiceAnimationPhase.Complete;
        }

        /// <summary>
        /// Skip to end of animation
        /// </summary>
        public void SkipToEnd()
        {
            if (!_isAnimating) return;

            _displayNumber = _finalResult;
            _phase = DiceAnimationPhase.Complete;
            _isAnimating = false;
            OnAnimationComplete?.Invoke(_rollResult);
        }
    }

    /// <summary>
    /// Phases of dice animation
    /// </summary>
    public enum DiceAnimationPhase
    {
        /// <summary>
        /// Dice is rapidly rolling
        /// </summary>
        Rolling,

        /// <summary>
        /// Dice is slowing down
        /// </summary>
        SlowingDown,

        /// <summary>
        /// Dice is settling on final number
        /// </summary>
        Settling,

        /// <summary>
        /// Animation is complete
        /// </summary>
        Complete
    }
}

