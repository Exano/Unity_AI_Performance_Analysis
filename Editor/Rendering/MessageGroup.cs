using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace FrameAnalyzer.Editor.Rendering
{
    public class MessageGroup : VisualElement
    {
        private readonly VisualElement _thinkingContainer;
        private readonly Foldout _toolsFoldout;
        private readonly VisualElement _contentContainer;
        private readonly Label _resultLabel;

        private Label _streamingLabel;
        private Label _activeThinkingLabel;
        private readonly StringBuilder _streamingText = new StringBuilder();
        private readonly List<string> _thinkingEntries = new List<string>();
        private readonly List<string> _toolNames = new List<string>();
        private bool _finalized;

        public MessageGroup()
        {
            AddToClassList("message-group");

            _thinkingContainer = new VisualElement();
            _thinkingContainer.AddToClassList("thinking-container");
            _thinkingContainer.style.display = DisplayStyle.None;
            Add(_thinkingContainer);

            _toolsFoldout = new Foldout { text = "Tools", value = false };
            _toolsFoldout.AddToClassList("tools-foldout");
            _toolsFoldout.style.display = DisplayStyle.None;
            Add(_toolsFoldout);

            _contentContainer = new VisualElement();
            _contentContainer.AddToClassList("group-content");
            Add(_contentContainer);

            _resultLabel = new Label();
            _resultLabel.AddToClassList("group-result");
            _resultLabel.style.display = DisplayStyle.None;
            Add(_resultLabel);
        }

        public void AddThinking(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _thinkingContainer.style.display = DisplayStyle.Flex;

            if (_activeThinkingLabel != null)
            {
                _activeThinkingLabel.RemoveFromClassList("thinking-active");
                _activeThinkingLabel = null;
            }

            _thinkingEntries.Add(text);
            var item = new Label(text);
            item.AddToClassList("thinking-item");
            item.AddToClassList("thinking-active");
            item.selection.isSelectable = true;
            _thinkingContainer.Add(item);
            _activeThinkingLabel = item;
        }

        private void CollapseThinking()
        {
            if (_thinkingEntries.Count == 0) return;
            _activeThinkingLabel = null;
            _thinkingContainer.Clear();

            var foldout = new Foldout
            {
                text = $"Thinking ({_thinkingEntries.Count} steps)",
                value = false
            };
            foldout.AddToClassList("thinking-foldout");
            foreach (var entry in _thinkingEntries)
            {
                var item = new Label(entry);
                item.AddToClassList("thinking-item");
                item.selection.isSelectable = true;
                foldout.Add(item);
            }
            _thinkingContainer.Add(foldout);
        }

        public void AddToolUse(string toolName)
        {
            _toolNames.Add(toolName);
            var item = new Label($"\u2022 {toolName}");
            item.AddToClassList("tool-item");
            _toolsFoldout.Add(item);
            _toolsFoldout.text = $"Used {_toolNames.Count} tool{(_toolNames.Count != 1 ? "s" : "")}";
            _toolsFoldout.style.display = DisplayStyle.Flex;
        }

        public void UpdateToolDetail(string detail)
        {
            var tc = _toolsFoldout.contentContainer;
            if (tc.childCount > 0)
            {
                var last = tc[tc.childCount - 1] as Label;
                if (last != null)
                    last.text += $" \u2192 {detail}";
            }
        }

        public void AppendText(string text)
        {
            if (_finalized) return;
            if (_streamingLabel == null)
            {
                _streamingLabel = new Label();
                _streamingLabel.AddToClassList("md-paragraph");
                _streamingLabel.AddToClassList("streaming-text");
                _streamingLabel.selection.isSelectable = true;
                _contentContainer.Add(_streamingLabel);
            }
            _streamingText.Append(text);
            _streamingLabel.text = _streamingText.ToString();
        }

        public string Finalize()
        {
            if (_finalized) return _streamingText.ToString();
            _finalized = true;
            CollapseThinking();

            var rawText = _streamingText.ToString();
            if (rawText.Length == 0) return rawText;

            _contentContainer.Clear();
            var rendered = MarkdownRenderer.Render(rawText);
            _contentContainer.Add(rendered);

            return rawText;
        }

        public void SetResult(string info)
        {
            _resultLabel.text = info;
            _resultLabel.style.display = DisplayStyle.Flex;
        }
    }
}
