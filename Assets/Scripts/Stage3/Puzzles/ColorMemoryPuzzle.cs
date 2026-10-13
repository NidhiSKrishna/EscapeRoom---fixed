using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EscapeRoom.Core;
using EscapeRoom.UI;
using EscapeRoom.Interaction;

namespace EscapeRoom.Stage3.Puzzles
{
    public enum PuzzleState
    {
        Idle,
        PlayingSequence,
        WaitingForInput,
        Completed,
        Failed
    }

    public class ColorMemoryPuzzle : MonoBehaviour
    {
        [Header("Puzzle Settings")]
        [SerializeField] private ColorButton[] colorButtons;

        [SerializeField] private int sequenceLength = 4;

        public event Action OnPuzzleCompleted;

        public bool IsCompleted { get; private set; }

        public int[] CurrentSequence { get; private set; }

        private PuzzleState state = PuzzleState.Idle;

        private readonly List<ColorType> sequence =
            new List<ColorType>();

        private int inputIndex = 0;

        private void Start()
        {
            GenerateSequence();
            StartCoroutine(AutoStartRoutine());
        }

        private IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            StartPuzzle();
        }

        private void OnEnable()
        {
            SubscribeToButtons();
        }

        private void OnDisable()
        {
            UnsubscribeFromButtons();
        }

        public void ConfigureButtons(ColorButton[] buttons)
        {
            UnsubscribeFromButtons();

            colorButtons = buttons;

            SubscribeToButtons();
        }

        private void SubscribeToButtons()
        {
            if (colorButtons == null)
                return;

            foreach (ColorButton button in colorButtons)
            {
                if (button != null)
                {
                    button.OnButtonPressed -= HandleButtonPressed;
                    button.OnButtonPressed += HandleButtonPressed;
                }
            }
        }

        private void UnsubscribeFromButtons()
        {
            if (colorButtons == null)
                return;

            foreach (ColorButton button in colorButtons)
            {
                if (button != null)
                {
                    button.OnButtonPressed -= HandleButtonPressed;
                }
            }
        }

        private void GenerateSequence()
        {
            sequence.Clear();

            CurrentSequence = new int[sequenceLength];

            for (int i = 0; i < sequenceLength; i++)
            {
                ColorType randomColor =
                    (ColorType)UnityEngine.Random.Range(0, 4);

                sequence.Add(randomColor);

                CurrentSequence[i] = (int)randomColor;
            }
        }

        public void StartPuzzle()
        {
            if (
                state == PuzzleState.PlayingSequence ||
                state == PuzzleState.Completed
            )
            {
                return;
            }

            StartCoroutine(PlaySequenceRoutine());
        }

        private IEnumerator PlaySequenceRoutine()
        {
            state = PuzzleState.PlayingSequence;

            SetButtonsInput(false);

            inputIndex = 0;

            yield return new WaitForSeconds(1f);

            foreach (ColorType color in sequence)
            {
                ColorButton button = GetButton(color);

                if (button != null)
                {
                    button.PlayFlashAndTone();
                }

                yield return new WaitForSeconds(0.6f);
            }

            SetButtonsInput(true);

            state = PuzzleState.WaitingForInput;
        }

        private void HandleButtonPressed(ColorButton button)
        {
            if (
                state != PuzzleState.WaitingForInput ||
                IsCompleted
            )
            {
                return;
            }

            if (button == null)
                return;

            if (
                inputIndex < sequence.Count &&
                button.Type == sequence[inputIndex]
            )
            {
                button.PlayFlashAndTone();

                inputIndex++;

                if (inputIndex >= sequence.Count)
                {
                    CompletePuzzle();
                }
            }
            else
            {
                StartCoroutine(
                    FailRoutine(button)
                );
            }
        }

        private IEnumerator FailRoutine(
            ColorButton wrongButton
        )
        {
            state = PuzzleState.Failed;

            SetButtonsInput(false);

            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayErrorSound();
            }

            if (wrongButton != null)
            {
                wrongButton.PlayErrorFlash();
            }

            FeedbackHUD.ShowMessage(
                "Incorrect sequence!",
                new Color(
                    0.95f,
                    0.35f,
                    0.35f
                )
            );

            if (Stage3Timer.Instance != null)
            {
                Stage3Timer.Instance.ApplyPenalty(15f);
            }

            yield return new WaitForSeconds(1.5f);

            inputIndex = 0;

            StartCoroutine(
                PlaySequenceRoutine()
            );
        }

        private void CompletePuzzle()
        {
            state = PuzzleState.Completed;

            IsCompleted = true;

            SetButtonsInput(false);

            if (Stage3AudioManager.Instance != null)
            {
                Stage3AudioManager.Instance.PlayCorrectSound();
            }

            FeedbackHUD.ShowMessage(
                "Memory puzzle completed!",
                new Color(
                    0.40f,
                    0.95f,
                    0.45f
                )
            );

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.CompleteFlag(
                    "MEMORY_PUZZLE_COMPLETE"
                );
            }

            OnPuzzleCompleted?.Invoke();
        }

        private void SetButtonsInput(bool enabled)
        {
            if (colorButtons == null)
                return;

            foreach (ColorButton button in colorButtons)
            {
                if (button != null)
                {
                    button.InputEnabled = enabled;
                }
            }
        }

        private ColorButton GetButton(ColorType type)
        {
            if (colorButtons == null)
                return null;

            foreach (ColorButton button in colorButtons)
            {
                if (
                    button != null &&
                    button.Type == type
                )
                {
                    return button;
                }
            }

            return null;
        }

        public string GetColorName(int index)
        {
            ColorType type = (ColorType)index;

            return type
                .ToString()
                .ToUpper();
        }
    }
}