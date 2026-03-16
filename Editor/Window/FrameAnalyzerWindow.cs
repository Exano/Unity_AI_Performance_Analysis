using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using FrameAnalyzer.Editor.Capture;
using FrameAnalyzer.Editor.Claude;
using FrameAnalyzer.Editor.History;
using FrameAnalyzer.Editor.Rendering;
using FrameAnalyzer.Editor.SceneAnalysis;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;

namespace FrameAnalyzer.Editor.Window
{
    public class FrameAnalyzerWindow : EditorWindow
    {
        [MenuItem("Window/AI Performance and Frame Analysis %#j")]
        public static void ShowWindow()
        {
            var window = GetWindow<FrameAnalyzerWindow>();
            window.titleContent = new GUIContent("AI Perf Analysis");
            window.minSize = new Vector2(400, 300);
        }

        // ── Serialized state (survives domain reload) ──
        [SerializeField] private int _frameCount = 120;
        [SerializeField] private bool _includeSceneAnalysis = true;
        [SerializeField] private bool _useMcp = true;
        [SerializeField] private bool _skipPermissions;
        [SerializeField] private string _userNotes = "";
        [SerializeField] private string _lastReportMarkdown;
        [SerializeField] private string _lastResultInfo;
        [SerializeField] private int _compareIndexA = -1;
        [SerializeField] private int _compareIndexB = -1;

        // ── Non-serialized runtime state ──
        private CaptureOrchestrator _orchestrator;
        private ClaudeProcess _claude;
        private MessageGroup _currentGroup;
        private string _sceneSnapshot;
        private bool _isCapturing;
        private bool _isAnalyzing;
        private List<SessionHistory.HistoryEntry> _historyEntries;

        // Thinking animation
        private IVisualElementScheduledItem _thinkingAnim;
        private int _thinkingDots;
        private double _lastThinkingUpdate;

        // ── UI Elements ──
        private VisualElement _root;
        private VisualElement _progressContainer;
        private VisualElement _progressFill;
        private Label _progressLabel;
        private ScrollView _outputScroll;
        private Button _analyzeBtn;
        private Button _cancelBtn;
        private Button _compareBtn;
        private Button _exportReportBtn;
        private Label _statusLabel;
        private PopupField<string> _dropdownA;
        private PopupField<string> _dropdownB;

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.AddToClassList("root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.tonythedev.frame-analyzer/Editor/UI/FrameAnalyzerStyles.uss");
            if (styleSheet != null)
                _root.styleSheets.Add(styleSheet);

            BuildToolbar();
            BuildConfigPanel();
            BuildComparisonPanel();
            BuildProgressBar();
            BuildOutputArea();

            if (!string.IsNullOrEmpty(_lastReportMarkdown))
                RestoreReport();
        }

        // ── UI Construction ──

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            var title = new Label("AI Performance and Frame Analysis");
            title.AddToClassList("toolbar-title");
            toolbar.Add(title);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            _exportReportBtn = new Button(ExportReport) { text = "Export Report" };
            _exportReportBtn.AddToClassList("toolbar-btn");
            _exportReportBtn.SetEnabled(!string.IsNullOrEmpty(_lastReportMarkdown));
            toolbar.Add(_exportReportBtn);

            var exportDataBtn = new Button(ExportSessionData) { text = "Export Data" };
            exportDataBtn.AddToClassList("toolbar-btn");
            toolbar.Add(exportDataBtn);

            var clearBtn = new Button(ClearOutput) { text = "Clear" };
            clearBtn.AddToClassList("toolbar-btn");
            toolbar.Add(clearBtn);

            _root.Add(toolbar);
        }

        private void BuildConfigPanel()
        {
            var configPanel = new VisualElement();
            configPanel.AddToClassList("config-panel");

            // Frame count
            var frameRow = new VisualElement();
            frameRow.AddToClassList("config-row");
            var frameLabel = new Label("Frames:");
            frameLabel.AddToClassList("config-label");
            frameRow.Add(frameLabel);

            var frameGroup = new VisualElement();
            frameGroup.AddToClassList("frame-count-group");
            var slider = new SliderInt(30, 600) { value = _frameCount };
            slider.AddToClassList("frame-count-slider");
            var valueLabel = new Label($"{_frameCount}");
            valueLabel.AddToClassList("frame-count-value");
            slider.RegisterValueChangedCallback(evt =>
            {
                _frameCount = evt.newValue;
                valueLabel.text = $"{_frameCount}";
            });
            frameGroup.Add(slider);
            frameGroup.Add(valueLabel);
            frameRow.Add(frameGroup);
            configPanel.Add(frameRow);

            // Options row
            var optionsRow = new VisualElement();
            optionsRow.AddToClassList("config-row");

            var sceneToggle = new Toggle("Scene Analysis") { value = _includeSceneAnalysis };
            sceneToggle.AddToClassList("option-toggle");
            sceneToggle.RegisterValueChangedCallback(evt => _includeSceneAnalysis = evt.newValue);
            optionsRow.Add(sceneToggle);

            var mcpToggle = new Toggle("MCP") { value = _useMcp };
            mcpToggle.AddToClassList("option-toggle");
            mcpToggle.RegisterValueChangedCallback(evt => _useMcp = evt.newValue);
            optionsRow.Add(mcpToggle);

            var permsToggle = new Toggle("Skip Permissions") { value = _skipPermissions };
            permsToggle.AddToClassList("option-toggle");
            permsToggle.RegisterValueChangedCallback(evt => _skipPermissions = evt.newValue);
            optionsRow.Add(permsToggle);

            configPanel.Add(optionsRow);

            // Notes foldout
            var notesFoldout = new Foldout { text = "Analysis Notes", value = !string.IsNullOrEmpty(_userNotes) };
            notesFoldout.AddToClassList("notes-foldout");
            var notesHint = new Label("Tell the AI what to ignore or focus on. These notes are included in every analysis and comparison.");
            notesHint.AddToClassList("notes-hint");
            notesFoldout.Add(notesHint);
            var notesField = new TextField { multiline = true, value = _userNotes };
            notesField.AddToClassList("notes-field");
            notesField.RegisterValueChangedCallback(evt => _userNotes = evt.newValue);
            notesFoldout.Add(notesField);
            configPanel.Add(notesFoldout);

            // Buttons
            var btnRow = new VisualElement();
            btnRow.AddToClassList("config-row");

            _analyzeBtn = new Button(StartAnalysis) { text = "Analyze" };
            _analyzeBtn.AddToClassList("analyze-btn");
            btnRow.Add(_analyzeBtn);

            _cancelBtn = new Button(CancelAnalysis) { text = "Cancel" };
            _cancelBtn.AddToClassList("cancel-btn");
            _cancelBtn.style.display = DisplayStyle.None;
            btnRow.Add(_cancelBtn);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("status-message");
            _statusLabel.style.marginLeft = 8;
            btnRow.Add(_statusLabel);

            configPanel.Add(btnRow);
            _root.Add(configPanel);
        }

        private void BuildComparisonPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("compare-panel");

            RefreshHistory();
            var choices = BuildDropdownChoices();
            bool hasHistory = _historyEntries != null && _historyEntries.Count >= 2;

            // Session A (baseline / older)
            var labelA = new Label("Baseline:");
            labelA.AddToClassList("compare-label");
            panel.Add(labelA);

            int idxA = (_compareIndexA >= 0 && _compareIndexA < choices.Count) ? _compareIndexA : (choices.Count > 1 ? 1 : 0);
            _dropdownA = new PopupField<string>(choices, idxA);
            _dropdownA.AddToClassList("compare-dropdown");
            _dropdownA.RegisterValueChangedCallback(evt => _compareIndexA = _dropdownA.index);
            panel.Add(_dropdownA);

            // Session B (current / newer)
            var labelB = new Label("vs:");
            labelB.AddToClassList("compare-label");
            labelB.style.marginLeft = 6;
            panel.Add(labelB);

            int idxB = (_compareIndexB >= 0 && _compareIndexB < choices.Count) ? _compareIndexB : 0;
            _dropdownB = new PopupField<string>(choices, idxB);
            _dropdownB.AddToClassList("compare-dropdown");
            _dropdownB.RegisterValueChangedCallback(evt => _compareIndexB = _dropdownB.index);
            panel.Add(_dropdownB);

            var refreshBtn = new Button(RefreshComparisonDropdown) { text = "\u21BB" };
            refreshBtn.AddToClassList("toolbar-btn");
            refreshBtn.tooltip = "Refresh session list";
            panel.Add(refreshBtn);

            _compareBtn = new Button(StartComparison) { text = "Compare" };
            _compareBtn.AddToClassList("compare-btn");
            _compareBtn.SetEnabled(hasHistory);
            panel.Add(_compareBtn);

            _root.Add(panel);
        }

        private void BuildProgressBar()
        {
            _progressContainer = new VisualElement();
            _progressContainer.AddToClassList("progress-container");
            _progressContainer.style.display = DisplayStyle.None;

            var bar = new VisualElement();
            bar.AddToClassList("progress-bar");
            _progressFill = new VisualElement();
            _progressFill.AddToClassList("progress-fill");
            _progressFill.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
            bar.Add(_progressFill);
            _progressContainer.Add(bar);

            _progressLabel = new Label("Capturing...");
            _progressLabel.AddToClassList("progress-label");
            _progressContainer.Add(_progressLabel);

            _root.Add(_progressContainer);
        }

        private void BuildOutputArea()
        {
            _outputScroll = new ScrollView(ScrollViewMode.Vertical);
            _outputScroll.AddToClassList("output-scroll");
            _root.Add(_outputScroll);
        }

        // ── History / dropdown helpers ──

        private void RefreshHistory()
        {
            _historyEntries = SessionHistory.ListSessions();
        }

        private List<string> BuildDropdownChoices()
        {
            var choices = new List<string>();
            if (_historyEntries == null || _historyEntries.Count == 0)
            {
                choices.Add("(no sessions)");
                return choices;
            }
            foreach (var e in _historyEntries)
                choices.Add(e.Label);
            return choices;
        }

        private void RefreshComparisonDropdown()
        {
            RefreshHistory();
            var choices = BuildDropdownChoices();
            bool hasEnough = _historyEntries != null && _historyEntries.Count >= 2;

            if (_dropdownA != null)
            {
                _dropdownA.choices = choices;
                _dropdownA.index = choices.Count > 1 ? 1 : 0; // default: second newest (baseline)
            }
            if (_dropdownB != null)
            {
                _dropdownB.choices = choices;
                _dropdownB.index = 0; // default: newest (current)
            }
            _compareBtn?.SetEnabled(hasEnough);
        }

        // ── Domain reload restore ──

        private void RestoreReport()
        {
            var group = new MessageGroup();
            group.AppendText(_lastReportMarkdown);
            group.Finalize();
            if (!string.IsNullOrEmpty(_lastResultInfo))
                group.SetResult(_lastResultInfo);
            _outputScroll.Add(group);
            SetStatus("Analysis complete. (restored after reload)", false);
        }

        // ── Thinking animation ──

        private void StartThinkingAnimation()
        {
            _thinkingDots = 0;
            _lastThinkingUpdate = EditorApplication.timeSinceStartup;
            StopThinkingAnimation();
            _thinkingAnim = _root.schedule.Execute(UpdateThinkingAnimation).Every(400);
            UpdateThinkingAnimation();
        }

        private void UpdateThinkingAnimation()
        {
            _thinkingDots = (_thinkingDots + 1) % 4;
            var dots = new string('.', _thinkingDots);
            var pad = new string(' ', 3 - _thinkingDots);
            if (_statusLabel != null)
            {
                _statusLabel.text = $"Thinking{dots}{pad}";
                _statusLabel.RemoveFromClassList("error-message");
                _statusLabel.RemoveFromClassList("status-message");
                _statusLabel.AddToClassList("thinking-status");
            }
        }

        private void StopThinkingAnimation()
        {
            if (_thinkingAnim != null)
            {
                _thinkingAnim.Pause();
                _thinkingAnim = null;
            }
            _statusLabel?.RemoveFromClassList("thinking-status");
        }

        // ── Analysis flow ──

        private void StartAnalysis()
        {
            if (_isCapturing || _isAnalyzing)
            {
                SetStatus("Analysis already in progress.", true);
                return;
            }

            if (!Application.isPlaying)
            {
                SetStatus("Enter Play Mode first.", true);
                return;
            }

            if (!EnsureClaude()) return;

            _sceneSnapshot = null;
            if (_includeSceneAnalysis)
            {
                try
                {
                    var snap = SceneAnalyzer.CaptureSnapshot();
                    _sceneSnapshot = SceneAnalyzer.FormatSnapshot(snap);
                }
                catch (Exception e)
                {
                    AddStatusMessage($"Scene analysis failed: {e.Message}");
                }
            }

            var collectors = new List<IFrameDataCollector>
            {
                new CpuTimingCollector(),
                new MemoryCollector(),
                new GpuTimingCollector(),
                new UrpPassCollector(),
                new Collectors.RenderingStatsCollector()
            };

            _orchestrator = new CaptureOrchestrator(collectors);

            try
            {
                _orchestrator.StartCapture(_frameCount);
                _isCapturing = true;
                ShowProgress(true);
                SetStatus("Capturing frames...", false);
                SetBusy(true);
                EditorApplication.update += OnCaptureUpdate;
            }
            catch (Exception e)
            {
                SetStatus(e.Message, true);
            }
        }

        private void OnCaptureUpdate()
        {
            if (!_isCapturing || _orchestrator == null) return;

            if (!Application.isPlaying)
            {
                _orchestrator.Cancel();
                FinishCapture(cancelled: true);
                return;
            }

            bool done = _orchestrator.CaptureFrame();
            UpdateProgress(_orchestrator.Progress,
                $"Frame {_orchestrator.CurrentFrame}/{_orchestrator.TotalFrames}");

            if (done)
                FinishCapture(cancelled: false);
        }

        private void FinishCapture(bool cancelled)
        {
            EditorApplication.update -= OnCaptureUpdate;
            _isCapturing = false;
            ShowProgress(false);

            if (cancelled)
            {
                SetStatus("Capture cancelled.", false);
                SetBusy(false);
                return;
            }

            SetStatus("Analyzing profiler data...", false);
            PostCaptureAnalysis();
        }

        private void PostCaptureAnalysis()
        {
            var session = _orchestrator.Session;

            try
            {
                var hierarchy = ProfilerHierarchyAnalyzer.Analyze(session.Frames.Count);
                session.ProfilerHierarchy = hierarchy;
                if (hierarchy.WasCollected)
                    AddStatusMessage($"Profiler hierarchy: {hierarchy.TopBySelfTime.Count} top markers across {hierarchy.FramesAnalyzed} frames");
            }
            catch (Exception e)
            {
                AddStatusMessage($"Profiler hierarchy analysis failed: {e.Message}");
            }

            // Memory breakdown — enumerate all loaded assets by type and size
            try
            {
                var breakdown = MemoryBreakdownAnalyzer.Analyze();
                session.MemoryBreakdown = breakdown;
                if (breakdown.WasCollected)
                    AddStatusMessage($"Memory breakdown: {breakdown.ByCategory.Count} categories, {breakdown.TopAssets.Count} top assets");
            }
            catch (Exception e)
            {
                AddStatusMessage($"Memory breakdown failed: {e.Message}");
            }

            // Memory snapshot (for manual deep inspection in Memory Profiler window)
            try
            {
                var snapshotPath = MemorySnapshotCapture.TakeSnapshot();
                if (snapshotPath != null)
                {
                    if (session.ProfilerHierarchy == null)
                        session.ProfilerHierarchy = new ProfilerHierarchyData();
                    session.ProfilerHierarchy.MemorySnapshotPath = snapshotPath;
                }
            }
            catch (Exception e)
            {
                AddStatusMessage($"Memory snapshot failed: {e.Message}");
            }

            // Auto-save to history for future comparisons
            try
            {
                SessionHistory.Save(session);
                SessionHistory.Prune();
                RefreshComparisonDropdown();
            }
            catch (Exception e)
            {
                AddStatusMessage($"History save failed: {e.Message}");
            }

            SendToClaudeAnalysis();
        }

        private void SendToClaudeAnalysis()
        {
            var session = _orchestrator.Session;
            bool mcpAvailable = _useMcp && McpHelper.IsMcpAvailable();

            if (mcpAvailable)
            {
                var mcpResult = McpHelper.EnsureMcpRegistered(Application.dataPath + "/..");
                if (mcpResult != null)
                    AddStatusMessage(mcpResult);
            }

            string notes = string.IsNullOrWhiteSpace(_userNotes) ? null : _userNotes.Trim();
            string prompt = AnalysisPromptBuilder.Build(session, _sceneSnapshot, mcpAvailable, notes);
            int maxTurns = mcpAvailable ? 3 : 1;

            SendPromptToClaude(prompt, maxTurns);
        }

        // ── Comparison flow ──

        private void StartComparison()
        {
            if (_isCapturing || _isAnalyzing)
            {
                SetStatus("Analysis already in progress.", true);
                return;
            }

            if (_historyEntries == null || _historyEntries.Count < 2)
            {
                SetStatus("Need at least two saved sessions to compare.", true);
                return;
            }

            int idxA = _dropdownA.index;
            int idxB = _dropdownB.index;

            if (idxA < 0 || idxA >= _historyEntries.Count ||
                idxB < 0 || idxB >= _historyEntries.Count)
            {
                SetStatus("Select two sessions to compare.", true);
                return;
            }

            if (idxA == idxB)
            {
                SetStatus("Pick two different sessions to compare.", true);
                return;
            }

            if (!EnsureClaude()) return;

            var entryA = _historyEntries[idxA];
            var entryB = _historyEntries[idxB];

            CaptureSession sessionA, sessionB;
            try
            {
                sessionA = SessionHistory.Load(entryA.FilePath);
                sessionB = SessionHistory.Load(entryB.FilePath);
                if (sessionA == null || sessionB == null)
                {
                    SetStatus("Failed to load one or both sessions.", true);
                    return;
                }
                if (sessionA.Summary == null) sessionA.ComputeSummary();
                if (sessionB.Summary == null) sessionB.ComputeSummary();
            }
            catch (Exception e)
            {
                SetStatus($"Failed to load sessions: {e.Message}", true);
                return;
            }

            string notes = string.IsNullOrWhiteSpace(_userNotes) ? null : _userNotes.Trim();
            string prompt = ComparisonPromptBuilder.Build(
                sessionA, sessionB, entryA.Label, entryB.Label, notes);

            SetBusy(true);
            SendPromptToClaude(prompt, maxTurns: 1);
        }

        // ── Shared Claude send ──

        private void SendPromptToClaude(string prompt, int maxTurns)
        {
            _currentGroup = new MessageGroup();
            _outputScroll.Add(_currentGroup);

            _isAnalyzing = true;
            StartThinkingAnimation();
            _claude.SendPrompt(prompt, maxTurns: maxTurns, skipPermissions: _skipPermissions);
            EditorApplication.update += OnClaudeUpdate;
        }

        private bool _receivedFirstChunk;

        private void OnClaudeUpdate()
        {
            if (_claude == null) return;

            bool complete = false;
            while (_claude.TryDequeue(out var chunk))
            {
                // Stop thinking animation on first real content
                if (!_receivedFirstChunk && chunk.Type != ClaudeProcess.OutputChunk.Kind.System
                    && chunk.Type != ClaudeProcess.OutputChunk.Kind.Status)
                {
                    _receivedFirstChunk = true;
                    StopThinkingAnimation();
                    SetStatus("Receiving response...", false);
                }

                switch (chunk.Type)
                {
                    case ClaudeProcess.OutputChunk.Kind.Text:
                        _currentGroup?.AppendText(chunk.Text);
                        ScrollToBottom();
                        break;
                    case ClaudeProcess.OutputChunk.Kind.Thinking:
                        _currentGroup?.AddThinking(chunk.Text);
                        break;
                    case ClaudeProcess.OutputChunk.Kind.ToolUse:
                        _currentGroup?.AddToolUse(chunk.Text);
                        break;
                    case ClaudeProcess.OutputChunk.Kind.Status:
                        _currentGroup?.UpdateToolDetail(chunk.Text);
                        break;
                    case ClaudeProcess.OutputChunk.Kind.Result:
                        _lastResultInfo = chunk.Text;
                        _currentGroup?.SetResult(chunk.Text);
                        break;
                    case ClaudeProcess.OutputChunk.Kind.System:
                        if (chunk.Text != null && chunk.Text.StartsWith("[Error]"))
                            AddStatusMessage(chunk.Text);
                        break;
                    case ClaudeProcess.OutputChunk.Kind.Complete:
                        complete = true;
                        break;
                }
            }

            if (complete)
            {
                EditorApplication.update -= OnClaudeUpdate;
                StopThinkingAnimation();
                _receivedFirstChunk = false;
                var rawText = _currentGroup?.Finalize();
                _lastReportMarkdown = rawText;
                _isAnalyzing = false;
                SetStatus("Analysis complete.", false);
                SetBusy(false);
                _exportReportBtn?.SetEnabled(!string.IsNullOrEmpty(_lastReportMarkdown));
                ScrollToBottom();
            }
        }

        private void CancelAnalysis()
        {
            if (_isCapturing)
            {
                _orchestrator?.Cancel();
                FinishCapture(cancelled: true);
            }
            else if (_isAnalyzing)
            {
                _claude?.Cancel();
                EditorApplication.update -= OnClaudeUpdate;
                StopThinkingAnimation();
                _receivedFirstChunk = false;
                var rawText = _currentGroup?.Finalize();
                _lastReportMarkdown = rawText;
                _isAnalyzing = false;
                SetStatus("Cancelled.", false);
                SetBusy(false);
                _exportReportBtn?.SetEnabled(!string.IsNullOrEmpty(_lastReportMarkdown));
            }
        }

        // ── Export ──

        private void ExportReport()
        {
            if (string.IsNullOrEmpty(_lastReportMarkdown))
            {
                SetStatus("No report to export.", true);
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Report", "", "frame-analysis-report", "md");
            if (string.IsNullOrEmpty(path)) return;

            System.IO.File.WriteAllText(path, _lastReportMarkdown);
            SetStatus($"Report exported to {path}", false);
        }

        private void ExportSessionData()
        {
            if (_orchestrator?.Session == null)
            {
                SetStatus("No capture data to export.", true);
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export Session Data", "", "frame-analysis-data", "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = SessionSerializer.ToJson(_orchestrator.Session);
            System.IO.File.WriteAllText(path, json);
            SetStatus($"Data exported to {path}", false);
        }

        // ── Helpers ──

        private bool EnsureClaude()
        {
            _claude?.Dispose();
            _claude = new ClaudeProcess(Application.dataPath + "/..");
            if (!_claude.IsClaudeAvailable())
            {
                SetStatus("Claude CLI not found. Install: npm install -g @anthropic-ai/claude-code", true);
                return false;
            }
            return true;
        }

        private void SetBusy(bool busy)
        {
            _analyzeBtn.style.display = busy ? DisplayStyle.None : DisplayStyle.Flex;
            _compareBtn?.SetEnabled(!busy && _historyEntries != null && _historyEntries.Count >= 2);
            _cancelBtn.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ClearOutput()
        {
            _outputScroll.Clear();
            _lastReportMarkdown = null;
            _lastResultInfo = null;
            _exportReportBtn?.SetEnabled(false);
        }

        private void ShowProgress(bool show)
        {
            _progressContainer.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                _progressFill.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
                _progressLabel.text = "Starting capture...";
            }
        }

        private void UpdateProgress(float progress, string text)
        {
            _progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
            _progressLabel.text = text;
        }

        private void SetStatus(string text, bool isError)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = text;
            _statusLabel.RemoveFromClassList("error-message");
            _statusLabel.RemoveFromClassList("status-message");
            _statusLabel.RemoveFromClassList("thinking-status");
            _statusLabel.AddToClassList(isError ? "error-message" : "status-message");
        }

        private void AddStatusMessage(string text)
        {
            var label = new Label(text);
            label.AddToClassList("status-message");
            _outputScroll.Add(label);
        }

        private void ScrollToBottom()
        {
            _outputScroll.schedule.Execute(() =>
                _outputScroll.scrollOffset = new Vector2(0, _outputScroll.contentContainer.layout.height));
        }

        private void OnDestroy()
        {
            StopThinkingAnimation();
            EditorApplication.update -= OnCaptureUpdate;
            EditorApplication.update -= OnClaudeUpdate;
            _orchestrator?.Cancel();
            _claude?.Dispose();
        }
    }
}
