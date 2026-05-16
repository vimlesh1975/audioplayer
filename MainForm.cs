using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;

namespace AudioPlayer;

internal sealed class MainForm : Form
{
    private static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AudioPlayer");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
    private static readonly string[] AudioExtensions =
    [
        ".wav", ".mp3", ".mpeg", ".mpg", ".mp2", ".aiff", ".aif", ".wma", ".aac", ".m4a", ".mp4", ".flac"
    ];
    private const int StartupPlaylistSeedCount = 10;

    private readonly TextBox _mediaRootBox = new();
    private readonly TextBox _searchBox = new();
    private readonly Button _browseFolderButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _clearSearchButton = new();
    private readonly TreeView _folderTree = new();
    private readonly DataGridView _clipGrid = new();
    private readonly DataGridView _playlistGrid = new();
    private readonly Button _addToPlaylistButton = new();
    private readonly Button _cueClipButton = new();
    private readonly Button _playClipButton = new();
    private readonly Button _clipPauseButton = new();
    private readonly Button _clipStopButton = new();
    private readonly Button _cuePreviousClipButton = new();
    private readonly Button _playPreviousClipButton = new();
    private readonly Button _cueNextClipButton = new();
    private readonly Button _playNextClipButton = new();
    private readonly Button _playPlaylistButton = new();
    private readonly Button _cuePreviousPlaylistButton = new();
    private readonly Button _playPreviousPlaylistButton = new();
    private readonly Button _playNextPlaylistButton = new();
    private readonly Button _cueNextPlaylistButton = new();
    private readonly Button _stopPlaylistButton = new();
    private readonly Button _seekBackFiveButton = new();
    private readonly Button _seekBackOneButton = new();
    private readonly Button _seekForwardOneButton = new();
    private readonly Button _seekForwardFiveButton = new();
    private readonly Button _startPlaylistButton = new();
    private readonly Button _removePlaylistButton = new();
    private readonly Button _moveUpButton = new();
    private readonly Button _moveDownButton = new();
    private readonly Button _setPlaylistStartTimeButton = new();
    private readonly Button _clearPlaylistButton = new();
    private readonly Button _openPlaylistButton = new();
    private readonly Button _savePlaylistButton = new();
    private readonly Button _mainPlayButton = new();
    private readonly Button _mainPauseResumeButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _appTitleLabel = new();
    private readonly Label _headerStatusLabel = new();
    private readonly Label _systemLabel = new();
    private readonly CheckBox _darkModeSwitch = new();
    private readonly Label _fileLabel = new();
    private readonly Label _timeLabel = new();
    private readonly Label _largeTimeLabel = new();
    private readonly Label _scrubStartLabel = new();
    private readonly Label _scrubDurationLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _clipCountLabel = new();
    private readonly ComboBox _audioDeviceBox = new();
    private readonly AudioMeterBar _leftMeter = new();
    private readonly AudioMeterBar _rightMeter = new();
    private readonly VerticalVolumeBar _volumeBar = new();
    private readonly WaveformControl _waveform = new();
    private readonly TimelineRulerControl _timelineRuler = new();
    private readonly System.Windows.Forms.Timer _positionTimer = new();
    private readonly ToolTip _toolTip = new();
    private readonly ContextMenuStrip _clipContextMenu = new();
    private readonly ContextMenuStrip _playlistContextMenu = new();
    private readonly List<PlaylistItem> _playlist = [];
    private AppSettings _settings = new();

    private AudioFileReader? _reader;
    private IWavePlayer? _output;
    private MeteringSampleProvider? _meteringProvider;
    private VolumeSampleProvider? _volumeProvider;
    private string? _mediaRoot;
    private string? _currentFolder;
    private string? _currentFile;
    private bool _seekingFromWaveform;
    private bool _playlistPlaybackActive;
    private bool _settingsReady;
    private int _currentPlaylistIndex = -1;
    private int? _clipDragRowIndex;
    private Point _clipDragStartPoint;
    private int? _playlistDragRowIndex;
    private Point _playlistDragStartPoint;

    public MainForm()
    {
        Text = "AudioPlayer";
        MinimumSize = new Size(1360, 820);
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 27, 31);
        ForeColor = Color.FromArgb(233, 238, 241);
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        ApplyToolTips();
        WireEvents();
        LoadAppSettings();
        PopulateAudioDevices();
        _darkModeSwitch.Checked = _settings.DarkMode;
        _volumeBar.Value = Math.Clamp(_settings.Volume, 0f, 5f);
        ApplyTheme(_darkModeSwitch.Checked);
        RestoreMediaRoot();
        RestorePlaylistOrSeed();
        _settingsReady = true;
        UpdateTransportState();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveAppSettings();
        CleanupPlayback();
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 2,
            ColumnCount = 1,
            BackColor = BackColor,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2,
            BackColor = BackColor,
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51));
        root.Controls.Add(main, 0, 1);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = BackColor,
            Margin = new Padding(0, 0, 12, 0),
        };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        left.Controls.Add(BuildPlaylistPanel(), 0, 0);
        left.Controls.Add(BuildMediaBrowser(), 0, 1);
        main.Controls.Add(left, 0, 0);
        main.Controls.Add(BuildPreviewPanel(), 1, 0);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            BackColor = BackColor,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        _appTitleLabel.Text = "AudioPlayer";
        _appTitleLabel.Dock = DockStyle.Fill;
        _appTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _appTitleLabel.Font = new Font(Font.FontFamily, 17f, FontStyle.Bold);
        _appTitleLabel.ForeColor = Color.FromArgb(238, 243, 246);

        _headerStatusLabel.Text = "Ready";
        _headerStatusLabel.Dock = DockStyle.Fill;
        _headerStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _headerStatusLabel.ForeColor = Color.FromArgb(112, 231, 166);

        _systemLabel.Text = "PC AUDIO";
        _systemLabel.Dock = DockStyle.Fill;
        _systemLabel.TextAlign = ContentAlignment.MiddleRight;
        _systemLabel.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
        _systemLabel.ForeColor = Color.FromArgb(137, 239, 194);

        _darkModeSwitch.Text = "Dark Mode";
        _darkModeSwitch.Checked = true;
        _darkModeSwitch.AutoSize = true;
        _darkModeSwitch.Dock = DockStyle.Fill;
        _darkModeSwitch.TextAlign = ContentAlignment.MiddleLeft;
        _darkModeSwitch.ForeColor = Color.FromArgb(235, 241, 244);

        ConfigureComboBox(_audioDeviceBox);
        _audioDeviceBox.Dock = DockStyle.Fill;
        _audioDeviceBox.Margin = new Padding(0, 14, 12, 12);
        _audioDeviceBox.Width = 280;

        header.Controls.Add(_appTitleLabel, 0, 0);
        header.Controls.Add(_headerStatusLabel, 1, 0);
        header.Controls.Add(_systemLabel, 2, 0);
        header.Controls.Add(MakeFieldLabel("Audio Device"), 3, 0);
        header.Controls.Add(_audioDeviceBox, 4, 0);
        header.Controls.Add(_darkModeSwitch, 5, 0);
        return header;
    }

    private Control BuildMediaBrowser()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.FromArgb(30, 35, 40),
            Padding = new Padding(16, 8, 16, 12),
            Margin = new Padding(0),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, Margin = new Padding(0, 0, 0, 8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        panel.Controls.Add(top, 0, 0);

        ConfigureTextBox(_mediaRootBox);
        ConfigureTextBox(_searchBox);
        _mediaRootBox.ReadOnly = true;
        _searchBox.PlaceholderText = "Search clips";
        ConfigureButton(_clearSearchButton, "Clear", 76);
        ConfigureButton(_browseFolderButton, "Browse", 76, ButtonRole.Primary);
        ConfigureButton(_refreshButton, "Refresh", 82);
        ConfigureButton(_addToPlaylistButton, "Add", 76);
        _clipCountLabel.Dock = DockStyle.Fill;
        _clipCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        _clipCountLabel.ForeColor = Color.FromArgb(166, 214, 245);
        _clipCountLabel.Text = "0 clips";
        top.Controls.Add(MakeFieldLabel("Location"), 0, 0);
        top.Controls.Add(_mediaRootBox, 1, 0);
        top.Controls.Add(_browseFolderButton, 2, 0);
        top.Controls.Add(_refreshButton, 3, 0);
        top.Controls.Add(MakeFieldLabel("Search Media"), 4, 0);
        top.Controls.Add(_searchBox, 5, 0);
        top.Controls.Add(_clearSearchButton, 6, 0);
        top.Controls.Add(_clipCountLabel, 7, 0);

        var browser = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(browser, 0, 1);

        _folderTree.Dock = DockStyle.Fill;
        _folderTree.HideSelection = false;
        _folderTree.BorderStyle = BorderStyle.FixedSingle;
        _folderTree.BackColor = Color.FromArgb(18, 21, 24);
        _folderTree.ForeColor = ForeColor;
        _folderTree.Margin = new Padding(0, 0, 10, 0);
        browser.Controls.Add(_folderTree, 0, 0);

        var clipPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0) };
        clipPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        clipPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        browser.Controls.Add(clipPanel, 1, 0);

        StyleGrid(_clipGrid);
        _clipGrid.Columns.Add(CreateTextColumn("Name", "File_Name", fill: true));
        _clipGrid.Columns.Add(CreateTextColumn("Duration", "Duration", width: 96));
        _clipGrid.Columns.Add(CreateTextColumn("Format", "Format", width: 78));
        _clipGrid.Columns.Add(CreateTextColumn("Channels", "Channels", width: 78));
        _clipGrid.Columns.Add(CreateTextColumn("Size", "File Size", width: 92));
        clipPanel.Controls.Add(_clipGrid, 0, 0);

        var transport = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        ConfigureButton(_cuePreviousClipButton, "Cue Prev", 82, ButtonRole.Primary);
        ConfigureButton(_playPreviousClipButton, "Play Prev", 86, ButtonRole.Primary);
        ConfigureButton(_cueClipButton, "Cue", 58);
        ConfigureButton(_playClipButton, "Play", 62, ButtonRole.Primary);
        ConfigureButton(_clipPauseButton, "Pause", 76, ButtonRole.Warning);
        ConfigureButton(_clipStopButton, "Stop", 64, ButtonRole.Danger);
        ConfigureButton(_cueNextClipButton, "Cue Next", 90, ButtonRole.Primary);
        ConfigureButton(_playNextClipButton, "Play Next", 96, ButtonRole.Primary);
        transport.Controls.AddRange([_cuePreviousClipButton, _playPreviousClipButton, _cueClipButton, _playClipButton, _clipPauseButton, _clipStopButton, _cueNextClipButton, _playNextClipButton]);
        clipPanel.Controls.Add(transport, 0, 1);

        return panel;
    }

    private Control BuildPlaylistPanel()
    {
        var panel = BuildSectionPanel("Playlist / Media");
        panel.RowStyles.Clear();
        panel.RowCount = 3;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        ConfigureButton(_startPlaylistButton, "Start Playlist", 104, ButtonRole.Success);
        ConfigureButton(_stopPlaylistButton, "Stop Playlist", 102, ButtonRole.Danger);
        ConfigureButton(_playPlaylistButton, "Play Row", 78, ButtonRole.Primary);
        ConfigureButton(_moveUpButton, "Up", 52);
        ConfigureButton(_moveDownButton, "Down", 64);
        ConfigureButton(_setPlaylistStartTimeButton, "Set Start", 82);
        ConfigureButton(_removePlaylistButton, "Remove", 76, ButtonRole.Danger);
        ConfigureButton(_clearPlaylistButton, "Clear", 62);
        ConfigureButton(_openPlaylistButton, "Open", 62);
        ConfigureButton(_savePlaylistButton, "Save", 62);
        controls.Controls.AddRange(
            [_startPlaylistButton, _stopPlaylistButton, _playPlaylistButton, _moveUpButton, _moveDownButton, _setPlaylistStartTimeButton, _removePlaylistButton, _clearPlaylistButton, _openPlaylistButton, _savePlaylistButton]);
        panel.Controls.Add(controls, 0, 1);

        StyleGrid(_playlistGrid);
        _playlistGrid.ReadOnly = false;
        _playlistGrid.AllowDrop = true;
        _playlistGrid.Columns.Add(CreateTextColumn("No", "#", width: 38));
        _playlistGrid.Columns.Add(CreateTextColumn("Start", "Start Time", width: 108));
        _playlistGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Play",
            HeaderText = "Play",
            Width = 48,
            ReadOnly = false,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _playlistGrid.Columns.Add(CreateTextColumn("Name", "Clip", fill: true));
        _playlistGrid.Columns.Add(CreateTextColumn("Duration", "Dur", width: 92));
        _playlistGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Loop",
            HeaderText = "Loop",
            Width = 46,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        panel.Controls.Add(_playlistGrid, 0, 2);

        return panel;
    }

    private Control BuildPreviewPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            BackColor = Color.FromArgb(30, 35, 40),
            Margin = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 144));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var previewHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, BackColor = Color.FromArgb(34, 40, 46) };
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        root.Controls.Add(previewHeader, 0, 0);

        _timeLabel.Dock = DockStyle.Fill;
        _timeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _timeLabel.Text = "00:00.000";
        _timeLabel.Font = new Font(Font.FontFamily, 14f, FontStyle.Bold);
        previewHeader.Controls.Add(_timeLabel, 0, 0);

        _fileLabel.AutoEllipsis = true;
        _fileLabel.Dock = DockStyle.Fill;
        _fileLabel.TextAlign = ContentAlignment.MiddleCenter;
        _fileLabel.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
        _fileLabel.ForeColor = Color.FromArgb(180, 192, 203);
        _fileLabel.Text = "No file loaded";
        previewHeader.Controls.Add(_fileLabel, 1, 0);

        var previewStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Black, Margin = new Padding(0, 0, 0, 8) };
        previewStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        previewStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.Controls.Add(previewStack, 0, 1);

        var previewFrame = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, BackColor = Color.Black };
        previewFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        previewFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previewFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        previewFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        previewStack.Controls.Add(previewFrame, 0, 0);

        ConfigureMeter(_leftMeter);
        ConfigureMeter(_rightMeter);
        previewFrame.Controls.Add(_leftMeter, 0, 0);
        _waveform.Dock = DockStyle.Fill;
        _waveform.Margin = new Padding(0);
        previewFrame.Controls.Add(_waveform, 1, 0);
        previewFrame.Controls.Add(_rightMeter, 2, 0);
        _volumeBar.Dock = DockStyle.Fill;
        _volumeBar.Margin = new Padding(8, 0, 0, 0);
        previewFrame.Controls.Add(_volumeBar, 3, 0);

        _timelineRuler.Dock = DockStyle.Fill;
        _timelineRuler.Margin = new Padding(42, 0, 108, 0);
        previewStack.Controls.Add(_timelineRuler, 0, 1);

        _largeTimeLabel.Dock = DockStyle.Fill;
        _largeTimeLabel.TextAlign = ContentAlignment.MiddleCenter;
        _largeTimeLabel.Font = new Font(Font.FontFamily, 14f, FontStyle.Bold);
        _largeTimeLabel.Text = "00:00.000";
        _largeTimeLabel.Margin = new Padding(0, 4, 0, 4);
        root.Controls.Add(_largeTimeLabel, 0, 2);

        var transport = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0, 6, 0, 0), Margin = new Padding(0, 2, 0, 8) };
        transport.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        transport.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.Controls.Add(transport, 0, 3);

        var scrubRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        scrubRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        scrubRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scrubRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        ConfigureTinyTimeLabel(_scrubStartLabel, "00:00:00.00", ContentAlignment.MiddleCenter);
        ConfigureTinyTimeLabel(_scrubDurationLabel, "00:00:00.00", ContentAlignment.MiddleCenter);
        scrubRow.Controls.Add(_scrubStartLabel, 0, 0);
        var progressPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(83, 97, 108), Margin = new Padding(6, 14, 6, 10) };
        scrubRow.Controls.Add(progressPanel, 1, 0);
        scrubRow.Controls.Add(_scrubDurationLabel, 2, 0);
        transport.Controls.Add(scrubRow, 0, 0);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        ConfigureButton(_cuePreviousPlaylistButton, "Prev Cue", 84);
        ConfigureButton(_playPreviousPlaylistButton, "Prev Play", 88, ButtonRole.Primary);
        ConfigureButton(_seekBackFiveButton, "-5 sec", 66);
        ConfigureButton(_seekBackOneButton, "-1 sec", 66);
        ConfigureButton(_mainPlayButton, "Play", 66, ButtonRole.Primary);
        ConfigureButton(_mainPauseResumeButton, "Pause", 78, ButtonRole.Warning);
        ConfigureButton(_stopButton, "Stop", 66, ButtonRole.Danger);
        ConfigureButton(_seekForwardOneButton, "+1 sec", 70);
        ConfigureButton(_seekForwardFiveButton, "+5 sec", 70);
        ConfigureButton(_playNextPlaylistButton, "Next Play", 88, ButtonRole.Primary);
        ConfigureButton(_cueNextPlaylistButton, "Next Cue", 84);
        controls.Controls.AddRange(
        [
            _cuePreviousPlaylistButton,
            _playPreviousPlaylistButton,
            _seekBackFiveButton,
            _seekBackOneButton,
            _mainPlayButton,
            _mainPauseResumeButton,
            _stopButton,
            _seekForwardOneButton,
            _seekForwardFiveButton,
            _playNextPlaylistButton,
            _cueNextPlaylistButton,
        ]);
        transport.Controls.Add(controls, 0, 1);

        root.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 35, 40) }, 0, 4);

        _statusLabel.ForeColor = Color.FromArgb(172, 183, 190);
        _statusLabel.Text = "Ready";
        return root;
    }

    private void WireEvents()
    {
        _browseFolderButton.Click += (_, _) => BrowseMediaFolder();
        _refreshButton.Click += (_, _) => RefreshMediaRoot();
        _clearSearchButton.Click += (_, _) => _searchBox.Clear();
        _searchBox.TextChanged += (_, _) => RefreshClipGrid();
        _folderTree.BeforeExpand += FolderTreeBeforeExpand;
        _folderTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string path)
            {
                SelectFolder(path);
            }
        };
        _clipGrid.SelectionChanged += (_, _) => UpdateTransportState();
        _clipGrid.CellMouseDown += ClipGridCellMouseDown;
        _clipGrid.MouseDown += ClipGridMouseDown;
        _clipGrid.MouseMove += ClipGridMouseMove;
        _clipGrid.CellDoubleClick += ClipGridCellDoubleClick;
        _addToPlaylistButton.Click += (_, _) => AddSelectedClipToPlaylist();
        _cueClipButton.Click += (_, _) => CueSelectedClip();
        _playClipButton.Click += (_, _) => PlaySelectedClip();
        _clipPauseButton.Click += (_, _) => TogglePauseResume();
        _playPlaylistButton.Click += (_, _) => PlaySelectedPlaylistItem(manualList: false);
        _startPlaylistButton.Click += (_, _) => PlaySelectedPlaylistItem(manualList: true);
        _stopPlaylistButton.Click += (_, _) => StopPlaylistMode();
        _removePlaylistButton.Click += (_, _) => RemoveSelectedPlaylistItem();
        _moveUpButton.Click += (_, _) => MoveSelectedPlaylistItem(-1);
        _moveDownButton.Click += (_, _) => MoveSelectedPlaylistItem(1);
        _setPlaylistStartTimeButton.Click += (_, _) => SetSelectedPlaylistStartTime();
        _clearPlaylistButton.Click += (_, _) => ClearPlaylist();
        _openPlaylistButton.Click += (_, _) => OpenPlaylist();
        _savePlaylistButton.Click += (_, _) => SavePlaylist();
        _playlistGrid.SelectionChanged += (_, _) => UpdateTransportState();
        _playlistGrid.CellMouseDown += PlaylistGridCellMouseDown;
        _playlistGrid.CellContentClick += PlaylistGridCellContentClick;
        _playlistGrid.CurrentCellDirtyStateChanged += PlaylistGridCurrentCellDirtyStateChanged;
        _playlistGrid.CellValueChanged += PlaylistGridCellValueChanged;
        _playlistGrid.MouseDown += PlaylistGridMouseDown;
        _playlistGrid.MouseMove += PlaylistGridMouseMove;
        _playlistGrid.DragEnter += PlaylistGridDragEnter;
        _playlistGrid.DragOver += PlaylistGridDragOver;
        _playlistGrid.DragDrop += PlaylistGridDragDrop;
        _playlistGrid.CellDoubleClick += PlaylistGridCellDoubleClick;
        _cuePreviousClipButton.Click += (_, _) => CueRelativeClip(-1);
        _playPreviousClipButton.Click += (_, _) => PlayRelativeClip(-1);
        _cueNextClipButton.Click += (_, _) => CueRelativeClip(1);
        _playNextClipButton.Click += (_, _) => PlayRelativeClip(1);
        _cuePreviousPlaylistButton.Click += (_, _) => CueRelativePlaylistItem(-1);
        _playPreviousPlaylistButton.Click += (_, _) => PlayRelativePlaylistItem(-1);
        _playNextPlaylistButton.Click += (_, _) => PlayRelativePlaylistItem(1);
        _cueNextPlaylistButton.Click += (_, _) => CueRelativePlaylistItem(1);
        _mainPlayButton.Click += (_, _) => PlayLoadedFromStart();
        _mainPauseResumeButton.Click += (_, _) => TogglePauseResume();
        _stopButton.Click += (_, _) => StopPlayback();
        _clipStopButton.Click += (_, _) => StopPlayback();
        _seekBackFiveButton.Click += (_, _) => SeekRelative(TimeSpan.FromSeconds(-5));
        _seekBackOneButton.Click += (_, _) => SeekRelative(TimeSpan.FromSeconds(-1));
        _seekForwardOneButton.Click += (_, _) => SeekRelative(TimeSpan.FromSeconds(1));
        _seekForwardFiveButton.Click += (_, _) => SeekRelative(TimeSpan.FromSeconds(5));
        _waveform.SeekRequested += (_, progress) => SeekToProgress(progress);

        _positionTimer.Interval = 100;
        _positionTimer.Tick += (_, _) => RefreshPosition();
        _audioDeviceBox.SelectedIndexChanged += (_, _) => RestartOnSelectedAudioDevice();
        _volumeBar.ValueChanged += (_, _) =>
        {
            if (_volumeProvider is not null)
            {
                _volumeProvider.Volume = _volumeBar.Value;
            }

            if (_settingsReady)
            {
                SaveAppSettings();
            }
        };
        _darkModeSwitch.CheckedChanged += (_, _) =>
        {
            ApplyTheme(_darkModeSwitch.Checked);
            if (_settingsReady)
            {
                SaveAppSettings();
            }
        };

        ConfigureContextMenus();
    }

    private void ApplyToolTips()
    {
        _toolTip.SetToolTip(_browseFolderButton, "Choose the media folder.");
        _toolTip.SetToolTip(_refreshButton, "Refresh the folder tree and clip list.");
        _toolTip.SetToolTip(_clearSearchButton, "Clear the clip search box.");
        _toolTip.SetToolTip(_addToPlaylistButton, "Add the selected clip to the playlist.");

        _toolTip.SetToolTip(_cuePreviousClipButton, "Cue the previous clip in the clip grid without playing.");
        _toolTip.SetToolTip(_playPreviousClipButton, "Play the previous clip in the clip grid.");
        _toolTip.SetToolTip(_cueClipButton, "Cue the selected clip without playing.");
        _toolTip.SetToolTip(_playClipButton, "Play the selected clip.");
        _toolTip.SetToolTip(_clipPauseButton, "Pause or resume playback.");
        _toolTip.SetToolTip(_clipStopButton, "Stop playback.");
        _toolTip.SetToolTip(_cueNextClipButton, "Cue the next clip in the clip grid without playing.");
        _toolTip.SetToolTip(_playNextClipButton, "Play the next clip in the clip grid.");

        _toolTip.SetToolTip(_startPlaylistButton, "Start playlist playback from the selected row.");
        _toolTip.SetToolTip(_stopPlaylistButton, "Stop playlist mode and prevent automatic playlist advance.");
        _toolTip.SetToolTip(_playPlaylistButton, "Play the selected playlist row only.");
        _toolTip.SetToolTip(_moveUpButton, "Move the selected playlist row up.");
        _toolTip.SetToolTip(_moveDownButton, "Move the selected playlist row down.");
        _toolTip.SetToolTip(_setPlaylistStartTimeButton, "Set schedule start time for the selected playlist row.");
        _toolTip.SetToolTip(_removePlaylistButton, "Remove the selected playlist row.");
        _toolTip.SetToolTip(_clearPlaylistButton, "Clear all playlist rows.");
        _toolTip.SetToolTip(_openPlaylistButton, "Open a saved playlist.");
        _toolTip.SetToolTip(_savePlaylistButton, "Save the playlist.");

        _toolTip.SetToolTip(_cuePreviousPlaylistButton, "Cue the previous playable playlist row without playing.");
        _toolTip.SetToolTip(_playPreviousPlaylistButton, "Play the previous playable playlist row.");
        _toolTip.SetToolTip(_mainPlayButton, "Play the loaded file from the start.");
        _toolTip.SetToolTip(_mainPauseResumeButton, "Pause or resume playback.");
        _toolTip.SetToolTip(_stopButton, "Stop playback.");
        _toolTip.SetToolTip(_seekBackFiveButton, "Seek backward 5 seconds.");
        _toolTip.SetToolTip(_seekBackOneButton, "Seek backward 1 second.");
        _toolTip.SetToolTip(_seekForwardOneButton, "Seek forward 1 second.");
        _toolTip.SetToolTip(_seekForwardFiveButton, "Seek forward 5 seconds.");
        _toolTip.SetToolTip(_playNextPlaylistButton, "Play the next playable playlist row.");
        _toolTip.SetToolTip(_cueNextPlaylistButton, "Cue the next playable playlist row without playing.");
        _toolTip.SetToolTip(_audioDeviceBox, "Select the PC audio output device.");
        _toolTip.SetToolTip(_volumeBar, "Set playback volume from 0.00 to 5.00. Default is 1.00.");
        _toolTip.SetToolTip(_darkModeSwitch, "Keep the app in dark mode.");
    }

    private void PopulateAudioDevices()
    {
        _audioDeviceBox.Items.Clear();
        _audioDeviceBox.Items.Add(new AudioDeviceItem(null, "Default audio device"));

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                _audioDeviceBox.Items.Add(new AudioDeviceItem(device.ID, device.FriendlyName));
            }
        }
        catch
        {
            // Keep the default device option if CoreAudio enumeration fails.
        }

        var restoredIndex = 0;
        if (!string.IsNullOrWhiteSpace(_settings.AudioDeviceId))
        {
            for (var i = 0; i < _audioDeviceBox.Items.Count; i++)
            {
                if (_audioDeviceBox.Items[i] is AudioDeviceItem item && string.Equals(item.DeviceId, _settings.AudioDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    restoredIndex = i;
                    break;
                }
            }
        }

        _audioDeviceBox.SelectedIndex = restoredIndex;
    }

    private void RestartOnSelectedAudioDevice()
    {
        if (_settingsReady)
        {
            SaveAppSettings();
        }

        if (_reader is null || _currentFile is null)
        {
            return;
        }

        var path = _currentFile;
        var position = _reader.CurrentTime;
        var shouldResume = _output?.PlaybackState == PlaybackState.Playing;

        try
        {
            CleanupPlayback();
            _reader = new AudioFileReader(path);
            _reader.CurrentTime = position < _reader.TotalTime ? position : TimeSpan.Zero;
            _output = CreateAudioOutput();
            _output.Init(CreateMeteredSampleProvider(_reader));
            _output.PlaybackStopped += OutputOnPlaybackStopped;
            _currentFile = path;

            if (shouldResume)
            {
                _output.Play();
                _positionTimer.Start();
            }

            RefreshPosition();
            UpdateTransportState();
            SetStatus("Audio device changed");
        }
        catch (Exception ex)
        {
            CleanupPlayback();
            MessageBox.Show(this, ex.Message, "Audio Device", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConfigureContextMenus()
    {
        _clipContextMenu.Items.Clear();
        _clipContextMenu.Items.Add(MakeMenuItem("Add to Playlist", (_, _) => AddSelectedClipToPlaylist()));
        _clipContextMenu.Items.Add(MakeMenuItem("Play Clip", (_, _) => PlaySelectedClip()));
        _clipContextMenu.Items.Add(new ToolStripSeparator());
        _clipContextMenu.Items.Add(MakeMenuItem("File Information", (_, _) => ShowSelectedClipInformation()));
        _clipContextMenu.Items.Add(MakeMenuItem("Open File Location", (_, _) => OpenSelectedClipLocation()));
        _clipContextMenu.Items.Add(MakeMenuItem("Refresh Folder", (_, _) => RefreshMediaRoot()));
        _clipContextMenu.Opening += (_, e) =>
        {
            var hasClip = GetSelectedClipPath() is not null;
            foreach (ToolStripItem item in _clipContextMenu.Items)
            {
                if (item is not ToolStripSeparator)
                {
                    item.Enabled = hasClip || item.Text == "Refresh Folder";
                }
            }
        };
        _clipGrid.ContextMenuStrip = _clipContextMenu;

        _playlistContextMenu.Items.Clear();
        _playlistContextMenu.Items.Add(MakeMenuItem("Play", (_, _) => PlaySelectedPlaylistItem(manualList: false)));
        _playlistContextMenu.Items.Add(MakeMenuItem("Start Playlist From Row", (_, _) => PlaySelectedPlaylistItem(manualList: true)));
        _playlistContextMenu.Items.Add(MakeMenuItem("Stop Playlist Mode", (_, _) => StopPlaylistMode()));
        _playlistContextMenu.Items.Add(new ToolStripSeparator());
        _playlistContextMenu.Items.Add(MakeMenuItem("Move Up", (_, _) => MoveSelectedPlaylistItem(-1)));
        _playlistContextMenu.Items.Add(MakeMenuItem("Move Down", (_, _) => MoveSelectedPlaylistItem(1)));
        _playlistContextMenu.Items.Add(MakeMenuItem("Set Start Time", (_, _) => SetSelectedPlaylistStartTime()));
        _playlistContextMenu.Items.Add(MakeMenuItem("Remove", (_, _) => RemoveSelectedPlaylistItem()));
        _playlistContextMenu.Items.Add(new ToolStripSeparator());
        _playlistContextMenu.Items.Add(MakeMenuItem("Select All", (_, _) => SetAllPlaylistPlayEnabled(true)));
        _playlistContextMenu.Items.Add(MakeMenuItem("Deselect All", (_, _) => SetAllPlaylistPlayEnabled(false)));
        _playlistContextMenu.Items.Add(new ToolStripSeparator());
        _playlistContextMenu.Items.Add(MakeMenuItem("File Information", (_, _) => ShowSelectedPlaylistInformation()));
        _playlistContextMenu.Items.Add(MakeMenuItem("Open File Location", (_, _) => OpenSelectedPlaylistLocation()));
        _playlistContextMenu.Items.Add(MakeMenuItem("Clear Playlist", (_, _) => ClearPlaylist()));
        _playlistContextMenu.Opening += (_, e) =>
        {
            var index = GetSelectedPlaylistIndex();
            var hasSelection = index.HasValue && index.Value >= 0 && index.Value < _playlist.Count;
            foreach (ToolStripItem item in _playlistContextMenu.Items)
            {
                if (item is ToolStripSeparator)
                {
                    continue;
                }

                item.Enabled = item.Text is "Clear Playlist" or "Select All" or "Deselect All"
                    ? _playlist.Count > 0
                    : hasSelection;
                if (item.Text == "Move Up")
                {
                    item.Enabled = index is > 0;
                }
                else if (item.Text == "Move Down")
                {
                    item.Enabled = index.HasValue && index.Value < _playlist.Count - 1;
                }
                else if (item.Text == "Start Playlist From Row")
                {
                    item.Enabled = !_playlistPlaybackActive && hasSelection;
                }
                else if (item.Text == "Stop Playlist Mode")
                {
                    item.Enabled = _playlistPlaybackActive;
                }
            }
        };
        _playlistGrid.ContextMenuStrip = _playlistContextMenu;
    }

    private static ToolStripMenuItem MakeMenuItem(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += onClick;
        return item;
    }

    private void ClipGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
        {
            SelectGridRow(_clipGrid, e.RowIndex);
        }
    }

    private void ClipGridMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            _clipDragRowIndex = null;
            return;
        }

        var hit = _clipGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex >= 0)
        {
            _clipDragRowIndex = hit.RowIndex;
            _clipDragStartPoint = e.Location;
            SelectGridRow(_clipGrid, hit.RowIndex);
        }
        else
        {
            _clipDragRowIndex = null;
        }
    }

    private void ClipGridMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_clipDragRowIndex.HasValue)
        {
            return;
        }

        if (_clipDragRowIndex.Value < 0 || _clipDragRowIndex.Value >= _clipGrid.Rows.Count)
        {
            _clipDragRowIndex = null;
            return;
        }

        if (Math.Abs(e.X - _clipDragStartPoint.X) < SystemInformation.DragSize.Width / 2 &&
            Math.Abs(e.Y - _clipDragStartPoint.Y) < SystemInformation.DragSize.Height / 2)
        {
            return;
        }

        if (_clipGrid.Rows[_clipDragRowIndex.Value].Tag is not string path || !File.Exists(path))
        {
            _clipDragRowIndex = null;
            return;
        }

        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { path });
        data.SetData(typeof(string), path);
        _clipGrid.DoDragDrop(data, DragDropEffects.Copy);
        _clipDragRowIndex = null;
    }

    private void ClipGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        SelectGridRow(_clipGrid, e.RowIndex);
        AddSelectedClipToPlaylist();
    }

    private void PlaylistGridCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
        {
            SelectGridRow(_playlistGrid, e.RowIndex);
        }
    }

    private void PlaylistGridCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _playlistGrid.Columns[e.ColumnIndex].Name == "Play")
        {
            return;
        }

        SelectGridRow(_playlistGrid, e.RowIndex);
        PlaySelectedPlaylistItem(manualList: false);
    }

    private void PlaylistGridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _playlistGrid.Columns[e.ColumnIndex].Name == "Play")
        {
            _playlistGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void PlaylistGridCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (_playlistGrid.IsCurrentCellDirty && _playlistGrid.CurrentCell?.OwningColumn?.Name == "Play")
        {
            _playlistGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void PlaylistGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= _playlist.Count || _playlistGrid.Columns[e.ColumnIndex].Name != "Play")
        {
            return;
        }

        _playlist[e.RowIndex].PlayEnabled = Convert.ToBoolean(_playlistGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
        RefreshPlaylistGrid(e.RowIndex);
    }

    private void PlaylistGridMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            _playlistDragRowIndex = null;
            return;
        }

        var hit = _playlistGrid.HitTest(e.X, e.Y);
        if (hit.RowIndex >= 0 && hit.ColumnIndex >= 0 && _playlistGrid.Columns[hit.ColumnIndex].Name != "Play")
        {
            _playlistDragRowIndex = hit.RowIndex;
            _playlistDragStartPoint = e.Location;
            SelectGridRow(_playlistGrid, hit.RowIndex);
        }
        else
        {
            _playlistDragRowIndex = null;
        }
    }

    private void PlaylistGridMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_playlistDragRowIndex.HasValue)
        {
            return;
        }

        if (_playlistDragRowIndex.Value < 0 || _playlistDragRowIndex.Value >= _playlist.Count)
        {
            return;
        }

        if (Math.Abs(e.X - _playlistDragStartPoint.X) < SystemInformation.DragSize.Width / 2 &&
            Math.Abs(e.Y - _playlistDragStartPoint.Y) < SystemInformation.DragSize.Height / 2)
        {
            return;
        }

        _playlistGrid.DoDragDrop(_playlistDragRowIndex.Value, DragDropEffects.Move);
        _playlistDragRowIndex = null;
    }

    private void PlaylistGridDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = GetPlaylistDropEffect(e);
    }

    private void PlaylistGridDragOver(object? sender, DragEventArgs e)
    {
        e.Effect = GetPlaylistDropEffect(e);
    }

    private void PlaylistGridDragDrop(object? sender, DragEventArgs e)
    {
        var clientPoint = _playlistGrid.PointToClient(new Point(e.X, e.Y));
        var hit = _playlistGrid.HitTest(clientPoint.X, clientPoint.Y);

        if (e.Data?.GetData(typeof(int)) is int sourceIndex)
        {
            var targetIndex = hit.RowIndex >= 0 ? hit.RowIndex : _playlist.Count - 1;
            MovePlaylistItem(sourceIndex, targetIndex);
            _playlistDragRowIndex = null;
            return;
        }

        var paths = GetDroppedAudioPaths(e.Data);
        if (paths.Length > 0)
        {
            var insertIndex = hit.RowIndex >= 0 ? hit.RowIndex : _playlist.Count;
            AddToPlaylist(paths, insertIndex);
        }

        _playlistDragRowIndex = null;
    }

    private static DragDropEffects GetPlaylistDropEffect(DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(int)) == true)
        {
            return DragDropEffects.Move;
        }

        return GetDroppedAudioPaths(e.Data).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private static string[] GetDroppedAudioPaths(IDataObject? data)
    {
        if (data is null)
        {
            return [];
        }

        if (data.GetData(DataFormats.FileDrop) is string[] files)
        {
            return files.Where(path => File.Exists(path) && IsAudioFile(path)).ToArray();
        }

        if (data.GetData(typeof(string)) is string path && File.Exists(path) && IsAudioFile(path))
        {
            return [path];
        }

        return [];
    }

    private static void SelectGridRow(DataGridView grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
        {
            return;
        }

        grid.ClearSelection();
        grid.Rows[rowIndex].Selected = true;
        grid.CurrentCell = grid.Rows[rowIndex].Cells[0];
    }

    private void SetInitialMediaRoot()
    {
        var defaultRoot = Directory.Exists(@"C:\casparcg\_media\audio")
            ? @"C:\casparcg\_media\audio"
            : Directory.Exists(@"C:\casparcg\_media")
                ? @"C:\casparcg\_media"
            : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        SetMediaRoot(defaultRoot);
    }

    private void RestoreMediaRoot()
    {
        if (!string.IsNullOrWhiteSpace(_settings.MediaRoot) && Directory.Exists(_settings.MediaRoot))
        {
            SetMediaRoot(_settings.MediaRoot);
            RestoreSelectedFolder();
            return;
        }

        SetInitialMediaRoot();
    }

    private void RestoreSelectedFolder()
    {
        if (string.IsNullOrWhiteSpace(_settings.SelectedFolder) || !Directory.Exists(_settings.SelectedFolder))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_mediaRoot) &&
            !_settings.SelectedFolder.StartsWith(_mediaRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentFolder = _settings.SelectedFolder;
        RefreshClipGrid();
    }

    private void RestorePlaylistOrSeed()
    {
        _playlist.Clear();
        foreach (var item in _settings.Playlist.Where(item => !string.IsNullOrWhiteSpace(item.Path) && File.Exists(item.Path)))
        {
            _playlist.Add(new PlaylistItem(item.Path)
            {
                PlayEnabled = item.PlayEnabled,
                StartTimeOverride = TryParsePlaylistTime(item.StartTime, out var startTime) ? startTime : null,
            });
        }

        if (_playlist.Count > 0)
        {
            RefreshPlaylistGrid(Math.Clamp(_settings.SelectedPlaylistIndex, 0, _playlist.Count - 1));
            SetStatus("Playlist restored");
            return;
        }

        SeedStartupPlaylist();
    }

    private void BrowseMediaFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select audio media folder",
            SelectedPath = Directory.Exists(_mediaRoot) ? _mediaRoot : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetMediaRoot(dialog.SelectedPath);
            SaveAppSettings();
        }
    }

    private void SetMediaRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        _mediaRoot = path;
        _mediaRootBox.Text = path;
        _folderTree.Nodes.Clear();
        var node = CreateFolderNode(path);
        _folderTree.Nodes.Add(node);
        node.Expand();
        _folderTree.SelectedNode = node;
        SelectFolder(path);
    }

    private void LoadAppSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private void SaveAppSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var settings = new AppSettings
            {
                MediaRoot = _mediaRoot,
                SelectedFolder = _currentFolder,
                AudioDeviceId = _audioDeviceBox.SelectedItem is AudioDeviceItem item ? item.DeviceId : null,
                Volume = _volumeBar.Value,
                DarkMode = _darkModeSwitch.Checked,
                SelectedPlaylistIndex = GetSelectedPlaylistIndex() ?? -1,
                Playlist = _playlist.Select(item => new PlaylistFileItem
                {
                    Path = item.Path,
                    PlayEnabled = item.PlayEnabled,
                    StartTime = item.StartTimeOverride.HasValue ? FormatPlaylistTime(item.StartTimeOverride.Value) : null,
                }).ToList(),
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings should never block app shutdown.
        }
    }

    private void RefreshMediaRoot()
    {
        if (Directory.Exists(_mediaRoot))
        {
            SetMediaRoot(_mediaRoot);
        }
    }

    private TreeNode CreateFolderNode(string path)
    {
        var node = new TreeNode(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : path)
        {
            Tag = path,
        };

        if (HasSubdirectories(path))
        {
            node.Nodes.Add(new TreeNode("Loading..."));
        }

        return node;
    }

    private void FolderTreeBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is not { } node)
        {
            return;
        }

        if (node.Nodes.Count == 1 && node.Nodes[0].Tag is null && node.Tag is string path)
        {
            node.Nodes.Clear();
            foreach (var directory in SafeEnumerateDirectories(path))
            {
                node.Nodes.Add(CreateFolderNode(directory));
            }
        }
    }

    private void SelectFolder(string path)
    {
        _currentFolder = path;
        RefreshClipGrid();
        if (_settingsReady)
        {
            SaveAppSettings();
        }
    }

    private void RefreshClipGrid()
    {
        _clipGrid.Rows.Clear();
        if (!Directory.Exists(_currentFolder))
        {
            return;
        }

        var search = _searchBox.Text.Trim();
        var files = SafeEnumerateFiles(_currentFolder)
            .Where(IsAudioFile)
            .Where(path => search.Length == 0 || Path.GetFileName(path).Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var metadata = ReadAudioMetadata(file);
            var rowIndex = _clipGrid.Rows.Add(
                Path.GetFileName(file),
                metadata.Duration,
                Path.GetExtension(file).TrimStart('.').ToUpperInvariant(),
                metadata.Channels,
                FormatFileSize(file));
            _clipGrid.Rows[rowIndex].Tag = file;
            _clipGrid.Rows[rowIndex].Cells["Name"].ToolTipText = file;
        }

        var countText = $"{_clipGrid.Rows.Count} clips";
        _clipCountLabel.Text = countText;
        SetStatus(countText);
        UpdateTransportState();
    }

    private void AddSelectedClipToPlaylist()
    {
        var path = GetSelectedClipPath();
        if (path is null)
        {
            return;
        }

        AddToPlaylist(path);
    }

    private void AddToPlaylist(string path)
    {
        _playlist.Add(new PlaylistItem(path));
        RefreshPlaylistGrid(_playlist.Count - 1);
        SetStatus("Added to playlist");
    }

    private void AddToPlaylist(IEnumerable<string> paths, int insertIndex)
    {
        var audioPaths = paths.Where(path => File.Exists(path) && IsAudioFile(path)).ToArray();
        if (audioPaths.Length == 0)
        {
            return;
        }

        insertIndex = Math.Clamp(insertIndex, 0, _playlist.Count);
        _playlist.InsertRange(insertIndex, audioPaths.Select(path => new PlaylistItem(path)));
        RefreshPlaylistGrid(insertIndex);
        SetStatus(audioPaths.Length == 1 ? "Added to playlist" : $"{audioPaths.Length} clips added to playlist");
    }

    private void SeedStartupPlaylist()
    {
        if (_playlist.Count > 0 || !Directory.Exists(_currentFolder))
        {
            return;
        }

        var files = SafeEnumerateFiles(_currentFolder)
            .Where(IsAudioFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Take(StartupPlaylistSeedCount)
            .ToArray();

        if (files.Length == 0)
        {
            return;
        }

        foreach (var file in files)
        {
            _playlist.Add(new PlaylistItem(file));
        }

        RefreshPlaylistGrid(0);
        SetStatus($"Seeded playlist with {files.Length} files");
    }

    private void CueSelectedClip()
    {
        var path = GetSelectedClipPath();
        if (path is null)
        {
            return;
        }

        if (!_playlistPlaybackActive)
        {
            _currentPlaylistIndex = -1;
        }

        LoadFile(path, autoPlay: false);
        RefreshPlaylistGrid();
        SetStatus("Clip cued");
    }

    private void PlaySelectedClip()
    {
        var path = GetSelectedClipPath();
        if (path is null)
        {
            return;
        }

        if (!_playlistPlaybackActive)
        {
            _currentPlaylistIndex = -1;
        }

        LoadFile(path, autoPlay: true);
        RefreshPlaylistGrid();
    }

    private void CueRelativeClip(int direction)
    {
        var index = FindRelativeClipIndex(direction);
        if (index is null)
        {
            SetStatus(direction > 0 ? "No next clip" : "No previous clip");
            return;
        }

        SelectGridRow(_clipGrid, index.Value);
        CueSelectedClip();
    }

    private void PlayRelativeClip(int direction)
    {
        var index = FindRelativeClipIndex(direction);
        if (index is null)
        {
            SetStatus(direction > 0 ? "No next clip" : "No previous clip");
            return;
        }

        SelectGridRow(_clipGrid, index.Value);
        PlaySelectedClip();
    }

    private int? FindRelativeClipIndex(int direction)
    {
        if (_clipGrid.Rows.Count == 0)
        {
            return null;
        }

        var selected = GetSelectedClipIndex() ?? (direction > 0 ? -1 : _clipGrid.Rows.Count);
        var next = selected + direction;
        return next >= 0 && next < _clipGrid.Rows.Count ? next : null;
    }

    private void PlaySelectedPlaylistItem(bool manualList)
    {
        var index = GetSelectedPlaylistIndex();
        if (index is null || index.Value < 0 || index.Value >= _playlist.Count)
        {
            return;
        }

        if (!_playlist[index.Value].PlayEnabled)
        {
            SetStatus("Playlist row is unchecked");
            return;
        }

        if (manualList)
        {
            _playlistPlaybackActive = true;
        }

        _currentPlaylistIndex = index.Value;
        LoadFile(_playlist[index.Value].Path, autoPlay: true);
        RefreshPlaylistGrid(index.Value);
    }

    private void CueRelativePlaylistItem(int direction)
    {
        var index = FindRelativePlaylistIndex(direction);
        if (index is null)
        {
            SetStatus(direction > 0 ? "No next playlist row" : "No previous playlist row");
            return;
        }

        CuePlaylistItem(index.Value);
    }

    private void PlayRelativePlaylistItem(int direction)
    {
        var index = FindRelativePlaylistIndex(direction);
        if (index is null)
        {
            SetStatus(direction > 0 ? "No next playlist row" : "No previous playlist row");
            return;
        }

        _currentPlaylistIndex = index.Value;
        LoadFile(_playlist[index.Value].Path, autoPlay: true);
        RefreshPlaylistGrid(index.Value);
    }

    private void CuePlaylistItem(int index)
    {
        if (index < 0 || index >= _playlist.Count)
        {
            return;
        }

        if (!_playlist[index].PlayEnabled)
        {
            SetStatus("Playlist row is unchecked");
            return;
        }

        _currentPlaylistIndex = index;
        LoadFile(_playlist[index].Path, autoPlay: false);
        RefreshPlaylistGrid(index);
        SetStatus("Playlist row cued");
    }

    private void StopPlaylistMode()
    {
        _playlistPlaybackActive = false;
        SetStatus("Playlist mode stopped");
        RefreshPlaylistGrid(_currentPlaylistIndex >= 0 ? _currentPlaylistIndex : null);
        UpdateTransportState();
    }

    private int? FindRelativePlaylistIndex(int direction)
    {
        if (_playlist.Count == 0)
        {
            return null;
        }

        var reference = _currentPlaylistIndex >= 0
            ? _currentPlaylistIndex
            : GetSelectedPlaylistIndex() ?? (direction > 0 ? -1 : _playlist.Count);
        var next = reference + direction;

        while (next >= 0 && next < _playlist.Count)
        {
            if (_playlist[next].PlayEnabled && File.Exists(_playlist[next].Path))
            {
                return next;
            }

            next += direction;
        }

        return null;
    }

    private void RemoveSelectedPlaylistItem()
    {
        var index = GetSelectedPlaylistIndex();
        if (index is null)
        {
            return;
        }

        _playlist.RemoveAt(index.Value);
        if (_currentPlaylistIndex == index.Value)
        {
            _currentPlaylistIndex = -1;
        }
        else if (_currentPlaylistIndex > index.Value)
        {
            _currentPlaylistIndex--;
        }

        if (_playlist.Count == 0)
        {
            _playlistPlaybackActive = false;
        }

        RefreshPlaylistGrid(Math.Min(index.Value, _playlist.Count - 1));
    }

    private void MoveSelectedPlaylistItem(int direction)
    {
        var index = GetSelectedPlaylistIndex();
        if (index is null)
        {
            return;
        }

        var next = index.Value + direction;
        if (next < 0 || next >= _playlist.Count)
        {
            return;
        }

        (_playlist[index.Value], _playlist[next]) = (_playlist[next], _playlist[index.Value]);
        if (_currentPlaylistIndex == index.Value)
        {
            _currentPlaylistIndex = next;
        }
        else if (_currentPlaylistIndex == next)
        {
            _currentPlaylistIndex = index.Value;
        }

        RefreshPlaylistGrid(next);
    }

    private void MovePlaylistItem(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _playlist.Count || targetIndex < 0 || targetIndex >= _playlist.Count || sourceIndex == targetIndex)
        {
            return;
        }

        var item = _playlist[sourceIndex];
        _playlist.RemoveAt(sourceIndex);
        _playlist.Insert(targetIndex, item);

        if (_currentPlaylistIndex == sourceIndex)
        {
            _currentPlaylistIndex = targetIndex;
        }
        else if (_currentPlaylistIndex > sourceIndex && _currentPlaylistIndex <= targetIndex)
        {
            _currentPlaylistIndex--;
        }
        else if (_currentPlaylistIndex < sourceIndex && _currentPlaylistIndex >= targetIndex)
        {
            _currentPlaylistIndex++;
        }

        RefreshPlaylistGrid(targetIndex);
    }

    private void SetSelectedPlaylistStartTime()
    {
        var index = GetSelectedPlaylistIndex();
        if (index is null || index.Value < 0 || index.Value >= _playlist.Count)
        {
            return;
        }

        var current = GetComputedPlaylistStartTime(index.Value);
        var input = PromptForText("Set Start Time", "Start time (HH:MM:SS.ff)", FormatPlaylistTime(current));
        if (input is null)
        {
            return;
        }

        if (!TryParsePlaylistTime(input, out var startTime))
        {
            MessageBox.Show(this, "Enter time as HH:MM:SS.ff, MM:SS.ff, or seconds.", "Set Start Time", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _playlist[index.Value].StartTimeOverride = startTime;
        RefreshPlaylistGrid(index.Value);
        SetStatus("Start time set");
    }

    private void SetAllPlaylistPlayEnabled(bool enabled)
    {
        foreach (var item in _playlist)
        {
            item.PlayEnabled = enabled;
        }

        RefreshPlaylistGrid(GetSelectedPlaylistIndex());
        SetStatus(enabled ? "All playlist rows selected" : "All playlist rows deselected");
    }

    private TimeSpan GetComputedPlaylistStartTime(int targetIndex)
    {
        var startTime = TimeSpan.Zero;
        for (var i = 0; i < _playlist.Count && i <= targetIndex; i++)
        {
            if (_playlist[i].StartTimeOverride is TimeSpan overrideStartTime)
            {
                startTime = overrideStartTime;
            }

            if (i == targetIndex)
            {
                return startTime;
            }

            if (TryReadDuration(_playlist[i].Path, out var duration))
            {
                startTime += duration;
            }
        }

        return TimeSpan.Zero;
    }

    private void ClearPlaylist()
    {
        _playlistPlaybackActive = false;
        _currentPlaylistIndex = -1;
        _playlist.Clear();
        RefreshPlaylistGrid();
    }

    private void OpenSelectedClipLocation()
    {
        var path = GetSelectedClipPath();
        if (path is not null)
        {
            OpenFileLocation(path);
        }
    }

    private void ShowSelectedClipInformation()
    {
        var path = GetSelectedClipPath();
        if (path is not null)
        {
            ShowFileInformation(path);
        }
    }

    private void OpenSelectedPlaylistLocation()
    {
        var index = GetSelectedPlaylistIndex();
        if (index.HasValue && index.Value >= 0 && index.Value < _playlist.Count)
        {
            OpenFileLocation(_playlist[index.Value].Path);
        }
    }

    private void ShowSelectedPlaylistInformation()
    {
        var index = GetSelectedPlaylistIndex();
        if (index.HasValue && index.Value >= 0 && index.Value < _playlist.Count)
        {
            ShowFileInformation(_playlist[index.Value].Path);
        }
    }

    private void ShowFileInformation(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "File not found.", "File Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            var info = new FileInfo(path);
            var format = reader.WaveFormat;
            var averageBitrate = reader.TotalTime.TotalSeconds > 0
                ? (info.Length * 8.0 / reader.TotalTime.TotalSeconds / 1000.0).ToString("0.##") + " kbps"
                : "--";

            ShowThemedInfoDialog(
                "File Information",
                [
                    ("File", Path.GetFileName(path)),
                    ("Path", path),
                    ("Duration", FormatPlaylistTime(reader.TotalTime)),
                    ("Format", Path.GetExtension(path).TrimStart('.').ToUpperInvariant()),
                    ("Channels", format.Channels.ToString()),
                    ("Sample rate", $"{format.SampleRate:N0} Hz"),
                    ("Bits per sample", format.BitsPerSample.ToString()),
                    ("Average bitrate", averageBitrate),
                    ("File size", FormatFileSize(path)),
                    ("Modified", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")),
                ]);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "File Information", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowThemedInfoDialog(string title, IReadOnlyList<(string Label, string Value)> rows)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(680, 390),
            BackColor = Color.FromArgb(24, 27, 31),
            ForeColor = Color.FromArgb(235, 241, 244),
            Font = Font,
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(16),
            BackColor = Color.FromArgb(24, 27, 31),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        dialog.Controls.Add(root);

        var heading = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 244, 247),
        };
        root.Controls.Add(heading, 0, 0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows.Count,
            BackColor = Color.FromArgb(18, 21, 24),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(grid, 0, 1);

        for (var i = 0; i < rows.Count; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows.Count));
            grid.Controls.Add(MakeInfoCell(rows[i].Label, bold: true), 0, i);
            grid.Controls.Add(MakeInfoCell(rows[i].Value, bold: false), 1, i);
        }

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        var close = new Button { DialogResult = DialogResult.OK };
        ConfigureButton(close, "Close", 82, ButtonRole.Primary);
        footer.Controls.Add(close);
        root.Controls.Add(footer, 0, 2);
        dialog.AcceptButton = close;

        dialog.ShowDialog(this);
    }

    private Label MakeInfoCell(string text, bool bold)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = Color.FromArgb(22, 26, 30),
            ForeColor = bold ? Color.FromArgb(166, 214, 245) : Color.FromArgb(235, 241, 244),
            Font = bold ? new Font(Font.FontFamily, Font.Size, FontStyle.Bold) : Font,
        };
    }

    private static void OpenFileLocation(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            ArgumentList = { "/select,", path },
            UseShellExecute = true,
        };
        process.Start();
    }

    private void OpenPlaylist()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open playlist",
            Filter = "Audio playlist (*.json)|*.json|All files|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var items = JsonSerializer.Deserialize<List<PlaylistFileItem>>(File.ReadAllText(dialog.FileName)) ?? [];
        _playlist.Clear();
        _playlist.AddRange(items.Where(item => !string.IsNullOrWhiteSpace(item.Path)).Select(item => new PlaylistItem(item.Path)
        {
            PlayEnabled = item.PlayEnabled,
            StartTimeOverride = TryParsePlaylistTime(item.StartTime, out var startTime) ? startTime : null,
        }));
        _playlistPlaybackActive = false;
        _currentPlaylistIndex = -1;
        RefreshPlaylistGrid();
        SetStatus("Playlist opened");
    }

    private void SavePlaylist()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Save playlist",
            Filter = "Audio playlist (*.json)|*.json|All files|*.*",
            DefaultExt = "json",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var items = _playlist.Select(item => new PlaylistFileItem
        {
            Path = item.Path,
            PlayEnabled = item.PlayEnabled,
            StartTime = item.StartTimeOverride.HasValue ? FormatPlaylistTime(item.StartTimeOverride.Value) : null,
        }).ToList();
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
        SetStatus("Playlist saved");
    }

    private void LoadFile(string path, bool autoPlay)
    {
        try
        {
            CleanupPlayback();
            SetStatus("Loading waveform...");
            Application.DoEvents();

            _reader = new AudioFileReader(path);
            _output = CreateAudioOutput();
            _output.Init(CreateMeteredSampleProvider(_reader));
            _output.PlaybackStopped += OutputOnPlaybackStopped;

            _currentFile = path;
            _fileLabel.Text = Path.GetFileName(path);
            _toolTip.SetToolTip(_fileLabel, path);
            _waveform.SetPeaks(WaveformAnalyzer.CreatePeaks(path, Math.Max(1200, _waveform.Width * 2)));
            SetStatus("Loaded");

            RefreshPosition();
            UpdateTransportState();

            if (autoPlay)
            {
                _output.Play();
                _positionTimer.Start();
                SetStatus("Playing");
                UpdateTransportState();
            }
        }
        catch (Exception ex)
        {
            CleanupPlayback();
            SetStatus("Could not open file");
            MessageBox.Show(this, ex.Message, "AudioPlayer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private IWavePlayer CreateAudioOutput()
    {
        if (_audioDeviceBox.SelectedItem is AudioDeviceItem { DeviceId: { Length: > 0 } deviceId })
        {
            using var enumerator = new MMDeviceEnumerator();
            return new WasapiOut(enumerator.GetDevice(deviceId), AudioClientShareMode.Shared, false, 100);
        }

        return new WasapiOut(AudioClientShareMode.Shared, 100);
    }

    private ISampleProvider CreateMeteredSampleProvider(AudioFileReader reader)
    {
        _volumeProvider = new VolumeSampleProvider(reader)
        {
            Volume = _volumeBar.Value,
        };
        _meteringProvider = new MeteringSampleProvider(_volumeProvider, 1024);
        _meteringProvider.StreamVolume += MeteringProviderStreamVolume;
        return _meteringProvider;
    }

    private void MeteringProviderStreamVolume(object? sender, StreamVolumeEventArgs e)
    {
        var left = e.MaxSampleValues.Length > 0 ? e.MaxSampleValues[0] : 0f;
        var right = e.MaxSampleValues.Length > 1 ? e.MaxSampleValues[1] : left;
        if (!IsDisposed && IsHandleCreated)
        {
            BeginInvoke(() => UpdateAudioMeters(left, right));
        }
    }

    private void UpdateAudioMeters(float left, float right)
    {
        _leftMeter.SetLevel(left);
        _rightMeter.SetLevel(right);
    }

    private void PlayLoadedFromStart()
    {
        if (_reader is null || _output is null)
        {
            PlaySelectedClip();
            return;
        }

        _reader.CurrentTime = TimeSpan.Zero;
        _output.Play();
        _positionTimer.Start();
        SetStatus("Playing");
        RefreshPosition();
        UpdateTransportState();
    }

    private void TogglePauseResume()
    {
        if (_reader is null || _output is null)
        {
            return;
        }

        if (_output.PlaybackState == PlaybackState.Playing)
        {
            _output.Pause();
            _positionTimer.Stop();
            SetStatus("Paused");
        }
        else
        {
            if (_reader.Position >= _reader.Length)
            {
                _reader.Position = 0;
            }

            _output.Play();
            _positionTimer.Start();
            SetStatus("Playing");
        }

        UpdateTransportState();
        RefreshPosition();
    }

    private void StopPlayback()
    {
        if (_reader is null || _output is null)
        {
            return;
        }

        _output.Stop();
        _reader.Position = 0;
        _positionTimer.Stop();
        SetStatus("Stopped");
        RefreshPosition();
        RefreshPlaylistGrid(_currentPlaylistIndex >= 0 ? _currentPlaylistIndex : null);
        UpdateTransportState();
    }

    private void SeekRelative(TimeSpan offset)
    {
        if (_reader is null)
        {
            return;
        }

        var target = _reader.CurrentTime + offset;
        if (target < TimeSpan.Zero)
        {
            target = TimeSpan.Zero;
        }
        else if (target > _reader.TotalTime)
        {
            target = _reader.TotalTime;
        }

        _reader.CurrentTime = target;
        RefreshPosition();
    }

    private void SeekToProgress(double progress)
    {
        if (_reader is null)
        {
            return;
        }

        _seekingFromWaveform = true;
        try
        {
            var targetTicks = (long)(_reader.TotalTime.Ticks * Math.Clamp(progress, 0, 1));
            _reader.CurrentTime = TimeSpan.FromTicks(targetTicks);
            RefreshPosition();
        }
        finally
        {
            _seekingFromWaveform = false;
        }
    }

    private void RefreshPosition()
    {
        if (_reader is null)
        {
            SetLabelText(_timeLabel, "00:00.000");
            SetLabelText(_largeTimeLabel, "00:00.000");
            SetLabelText(_scrubStartLabel, "00:00:00.00");
            SetLabelText(_scrubDurationLabel, "00:00:00.00");
            _timelineRuler.Duration = TimeSpan.Zero;
            _waveform.Progress = 0;
            UpdateAudioMeters(0, 0);
            return;
        }

        var duration = _reader.TotalTime;
        var position = _reader.CurrentTime;
        var remaining = duration - position;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        SetLabelText(_timeLabel, FormatTime(remaining));
        SetLabelText(_largeTimeLabel, FormatTime(position));
        SetLabelText(_scrubStartLabel, "00:00:00.00");
        SetLabelText(_scrubDurationLabel, FormatPlaylistTime(duration));
        _timelineRuler.Duration = duration;

        if (!_seekingFromWaveform && duration.TotalSeconds > 0)
        {
            _waveform.Progress = position.TotalSeconds / duration.TotalSeconds;
        }
    }

    private void HandlePlaybackStopped()
    {
        if (IsDisposed || _reader is null || _output is null)
        {
            return;
        }

        _positionTimer.Stop();
        var endedNaturally = IsAtNaturalEnd();
        if (endedNaturally)
        {
            _reader.Position = 0;
            if (_playlistPlaybackActive && PlayNextPlaylistItem())
            {
                return;
            }

            SetStatus(_playlistPlaybackActive ? "Playlist finished" : "Finished");
        }

        RefreshPosition();
        RefreshPlaylistGrid(_currentPlaylistIndex >= 0 ? _currentPlaylistIndex : null);
        UpdateTransportState();
    }

    private bool PlayNextPlaylistItem()
    {
        var next = _currentPlaylistIndex + 1;
        while (next < _playlist.Count)
        {
            if (_playlist[next].PlayEnabled && File.Exists(_playlist[next].Path))
            {
                _playlistPlaybackActive = true;
                _currentPlaylistIndex = next;
                LoadFile(_playlist[next].Path, autoPlay: true);
                RefreshPlaylistGrid(next);
                return true;
            }

            next++;
        }

        return false;
    }

    private bool IsAtNaturalEnd()
    {
        if (_reader is null)
        {
            return false;
        }

        if (_reader.Position >= _reader.Length)
        {
            return true;
        }

        var remaining = _reader.TotalTime - _reader.CurrentTime;
        return remaining <= TimeSpan.FromMilliseconds(250);
    }

    private void OutputOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!IsDisposed && IsHandleCreated)
        {
            BeginInvoke(HandlePlaybackStopped);
        }
    }

    private void RefreshPlaylistGrid(int? selectedIndex = null)
    {
        _playlistGrid.Rows.Clear();
        var startTime = TimeSpan.Zero;
        for (var i = 0; i < _playlist.Count; i++)
        {
            var item = _playlist[i];
            if (item.StartTimeOverride.HasValue)
            {
                startTime = item.StartTimeOverride.Value;
            }

            var status = i == _currentPlaylistIndex && _output?.PlaybackState == PlaybackState.Playing
                ? "PLAYING"
                : !item.PlayEnabled
                    ? "SKIPPED"
                : !File.Exists(item.Path)
                    ? "MISSING"
                    : i == _currentPlaylistIndex + 1 && _playlistPlaybackActive
                        ? "NEXT"
                        : "READY";
            var duration = ReadDurationText(item.Path);
            var rowIndex = _playlistGrid.Rows.Add(i + 1, FormatPlaylistTime(startTime), item.PlayEnabled, Path.GetFileName(item.Path), duration, false);
            _playlistGrid.Rows[rowIndex].Tag = item.Path;
            ApplyPlaylistRowStyle(_playlistGrid.Rows[rowIndex], status, _darkModeSwitch.Checked);

            if (TryReadDuration(item.Path, out var itemDuration))
            {
                startTime += itemDuration;
            }
        }

        if (selectedIndex.HasValue && _playlistGrid.Rows.Count > 0)
        {
            var row = Math.Clamp(selectedIndex.Value, 0, _playlistGrid.Rows.Count - 1);
            _playlistGrid.ClearSelection();
            _playlistGrid.Rows[row].Selected = true;
            _playlistGrid.CurrentCell = _playlistGrid.Rows[row].Cells[0];
        }

        UpdateTransportState();
    }

    private void UpdateTransportState()
    {
        var hasFile = _reader is not null && _output is not null;
        var hasClip = GetSelectedClipPath() is not null;
        var clipIndex = GetSelectedClipIndex();
        var playlistIndex = GetSelectedPlaylistIndex();
        var hasPlaylistSelection = playlistIndex.HasValue && playlistIndex.Value >= 0 && playlistIndex.Value < _playlist.Count;
        var selectedPlaylistPlayable = hasPlaylistSelection && _playlist[playlistIndex!.Value].PlayEnabled;
        _startPlaylistButton.Text = _playlistPlaybackActive ? "Playlist ON" : "Start Playlist";
        _mainPlayButton.Text = "Play";
        _mainPauseResumeButton.Text = hasFile && _output?.PlaybackState == PlaybackState.Playing ? "Pause" : "Resume";
        _clipPauseButton.Text = hasFile && _output?.PlaybackState == PlaybackState.Playing ? "Pause" : "Resume";
        _stopButton.Enabled = hasFile;
        _clipStopButton.Enabled = hasFile;
        _mainPlayButton.Enabled = hasFile || hasClip;
        _mainPauseResumeButton.Enabled = hasFile;
        _clipPauseButton.Enabled = hasFile;
        _seekBackFiveButton.Enabled = hasFile;
        _seekBackOneButton.Enabled = hasFile;
        _seekForwardOneButton.Enabled = hasFile;
        _seekForwardFiveButton.Enabled = hasFile;
        _cueClipButton.Enabled = hasClip;
        _playClipButton.Enabled = hasClip;
        _addToPlaylistButton.Enabled = hasClip;
        _cuePreviousClipButton.Enabled = clipIndex is > 0;
        _playPreviousClipButton.Enabled = clipIndex is > 0;
        _cueNextClipButton.Enabled = clipIndex.HasValue && clipIndex.Value < _clipGrid.Rows.Count - 1;
        _playNextClipButton.Enabled = clipIndex.HasValue && clipIndex.Value < _clipGrid.Rows.Count - 1;
        _playPlaylistButton.Enabled = selectedPlaylistPlayable;
        _startPlaylistButton.Enabled = !_playlistPlaybackActive && selectedPlaylistPlayable;
        _stopPlaylistButton.Enabled = _playlistPlaybackActive;
        _cuePreviousPlaylistButton.Enabled = FindRelativePlaylistIndex(-1).HasValue;
        _playPreviousPlaylistButton.Enabled = FindRelativePlaylistIndex(-1).HasValue;
        _cueNextPlaylistButton.Enabled = FindRelativePlaylistIndex(1).HasValue;
        _playNextPlaylistButton.Enabled = FindRelativePlaylistIndex(1).HasValue;
        _removePlaylistButton.Enabled = hasPlaylistSelection;
        _moveUpButton.Enabled = playlistIndex is > 0;
        _moveDownButton.Enabled = playlistIndex.HasValue && playlistIndex.Value < _playlist.Count - 1;
        _setPlaylistStartTimeButton.Enabled = hasPlaylistSelection;
        _clearPlaylistButton.Enabled = _playlist.Count > 0;
        _savePlaylistButton.Enabled = _playlist.Count > 0;
    }

    private string? GetSelectedClipPath()
    {
        return _clipGrid.SelectedRows.Count > 0 ? _clipGrid.SelectedRows[0].Tag as string : null;
    }

    private int? GetSelectedClipIndex()
    {
        return _clipGrid.SelectedRows.Count > 0 ? _clipGrid.SelectedRows[0].Index : null;
    }

    private int? GetSelectedPlaylistIndex()
    {
        if (_playlistGrid.SelectedRows.Count == 0)
        {
            return null;
        }

        return _playlistGrid.SelectedRows[0].Index;
    }

    private void CleanupPlayback()
    {
        _positionTimer.Stop();

        if (_output is not null)
        {
            _output.PlaybackStopped -= OutputOnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        if (_meteringProvider is not null)
        {
            _meteringProvider.StreamVolume -= MeteringProviderStreamVolume;
            _meteringProvider = null;
        }
        _volumeProvider = null;

        _reader?.Dispose();
        _reader = null;
        _currentFile = null;
        UpdateAudioMeters(0, 0);
    }

    private TableLayoutPanel BuildSectionPanel(string title)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Color.FromArgb(30, 35, 40),
            Padding = new Padding(16, 10, 16, 12),
            Margin = new Padding(0, 0, 0, 10),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 244, 247),
        };
        panel.Controls.Add(titleLabel, 0, 0);
        return panel;
    }

    private static Label MakeFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(232, 238, 241),
            Margin = new Padding(0, 5, 6, 5),
        };
    }

    private static Label MakeTinyTimeLabel(string text, ContentAlignment alignment)
    {
        var label = new Label();
        ConfigureTinyTimeLabel(label, text, alignment);
        return label;
    }

    private static void ConfigureTinyTimeLabel(Label label, string text, ContentAlignment alignment)
    {
        label.Text = text;
        label.Dock = DockStyle.Fill;
        label.TextAlign = alignment;
        label.ForeColor = Color.FromArgb(205, 215, 222);
        label.Font = new Font("Consolas", 8.5f);
    }

    private string? PromptForText(string title, string label, string defaultValue)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(360, 126),
            BackColor = Color.FromArgb(30, 35, 40),
            ForeColor = Color.FromArgb(235, 241, 244),
            Font = Font,
        };

        var labelControl = new Label
        {
            Text = label,
            Left = 12,
            Top = 12,
            Width = 336,
            Height = 24,
        };
        var input = new TextBox
        {
            Text = defaultValue,
            Left = 12,
            Top = 40,
            Width = 336,
            BackColor = Color.FromArgb(18, 21, 24),
            ForeColor = Color.FromArgb(235, 241, 244),
            BorderStyle = BorderStyle.FixedSingle,
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 192,
            Top = 82,
            Width = 74,
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 274,
            Top = 82,
            Width = 74,
        };
        ConfigureButton(ok, "OK", 74, ButtonRole.Primary);
        ConfigureButton(cancel, "Cancel", 74);
        _toolTip.SetToolTip(ok, "Apply the entered start time.");
        _toolTip.SetToolTip(cancel, "Close without changing the start time.");

        dialog.Controls.AddRange([labelControl, input, ok, cancel]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
    }

    private static void ConfigureMeter(AudioMeterBar meter)
    {
        meter.Dock = DockStyle.Fill;
        meter.Margin = new Padding(1, 0, 1, 0);
    }

    private Button CreatePassiveButton(string text, int width, ButtonRole role = ButtonRole.Neutral)
    {
        var button = new Button { Enabled = false };
        ConfigureButton(button, text, width, role);
        _toolTip.SetToolTip(button, GetPassiveButtonToolTip(text));
        button.Enabled = false;
        return button;
    }

    private static string GetPassiveButtonToolTip(string text)
    {
        return text switch
        {
            "-5 sec" => "Seek back 5 seconds. Not enabled for full-file audio playback yet.",
            "-1 sec" => "Seek back 1 second. Not enabled for full-file audio playback yet.",
            "Pause" => "Pause playback.",
            "+1 sec" => "Seek forward 1 second. Not enabled for full-file audio playback yet.",
            "+5 sec" => "Seek forward 5 seconds. Not enabled for full-file audio playback yet.",
            _ => text,
        };
    }

    private void SetStatus(string text)
    {
        SetLabelText(_statusLabel, text);
        SetLabelText(_headerStatusLabel, text);
    }

    private static void SetLabelText(Label label, string text)
    {
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
        {
            label.Text = text;
        }
    }

    private void ApplyTheme(bool dark)
    {
        var back = dark ? Color.FromArgb(24, 27, 31) : Color.FromArgb(238, 241, 244);
        var panel = dark ? Color.FromArgb(30, 35, 40) : Color.FromArgb(225, 230, 235);
        var input = dark ? Color.FromArgb(18, 21, 24) : Color.White;
        var text = dark ? Color.FromArgb(233, 238, 241) : Color.FromArgb(25, 31, 36);
        var muted = dark ? Color.FromArgb(172, 183, 190) : Color.FromArgb(78, 88, 96);
        var header = dark ? Color.FromArgb(34, 40, 46) : Color.FromArgb(210, 218, 225);

        BackColor = back;
        ForeColor = text;
        ApplyThemeRecursive(this, dark, back, panel, input, text, muted, header);

        _waveform.BackColor = input;
        _waveform.ForeColor = dark ? Color.FromArgb(116, 215, 255) : Color.FromArgb(24, 101, 164);
        _timelineRuler.BackColor = dark ? Color.FromArgb(12, 15, 18) : Color.FromArgb(214, 222, 229);
        _timelineRuler.ForeColor = text;
        _leftMeter.BackColor = input;
        _rightMeter.BackColor = input;
        _volumeBar.BackColor = input;

        ApplyGridTheme(_clipGrid, dark);
        ApplyGridTheme(_playlistGrid, dark);
        _headerStatusLabel.ForeColor = dark ? Color.FromArgb(112, 231, 166) : Color.FromArgb(24, 128, 77);
        _systemLabel.ForeColor = dark ? Color.FromArgb(137, 239, 194) : Color.FromArgb(24, 128, 77);
        _fileLabel.ForeColor = muted;
        _statusLabel.ForeColor = muted;
        _clipCountLabel.ForeColor = dark ? Color.FromArgb(166, 214, 245) : Color.FromArgb(24, 101, 164);
        RefreshPlaylistGrid(GetSelectedPlaylistIndex());
    }

    private static void ApplyThemeRecursive(Control root, bool dark, Color back, Color panel, Color input, Color text, Color muted, Color header)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button:
                    break;
                case TextBox textBox:
                    textBox.BackColor = input;
                    textBox.ForeColor = text;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = input;
                    comboBox.ForeColor = text;
                    break;
                case TreeView tree:
                    tree.BackColor = input;
                    tree.ForeColor = text;
                    break;
                case DataGridView:
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = text;
                    checkBox.BackColor = panel;
                    break;
                case Label label:
                    label.ForeColor = text;
                    break;
                case TableLayoutPanel or FlowLayoutPanel or Panel:
                    control.BackColor = control.BackColor == Color.Black ? control.BackColor : panel;
                    break;
            }

            if (control.Controls.Count > 0)
            {
                ApplyThemeRecursive(control, dark, back, panel, input, text, muted, header);
            }
        }
    }

    private static void ApplyGridTheme(DataGridView grid, bool dark)
    {
        grid.BackgroundColor = dark ? Color.FromArgb(18, 21, 24) : Color.White;
        grid.GridColor = dark ? Color.FromArgb(48, 55, 62) : Color.FromArgb(190, 199, 207);
        grid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(35, 42, 49) : Color.FromArgb(210, 218, 225);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Color.FromArgb(235, 241, 244) : Color.FromArgb(25, 31, 36);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = grid.ColumnHeadersDefaultCellStyle.BackColor;
        grid.DefaultCellStyle.BackColor = dark ? Color.FromArgb(22, 26, 30) : Color.White;
        grid.DefaultCellStyle.ForeColor = dark ? Color.FromArgb(231, 236, 239) : Color.FromArgb(25, 31, 36);
        grid.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(58, 89, 118) : Color.FromArgb(58, 121, 184);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = dark ? Color.FromArgb(26, 30, 35) : Color.FromArgb(240, 244, 247);
    }

    private static void ConfigureButton(Button button, string text, int width)
    {
        ConfigureButton(button, text, width, ButtonRole.Neutral);
    }

    private static void ConfigureButton(Button button, string text, int width, ButtonRole role)
    {
        button.Text = text;
        button.Width = width;
        button.Height = 32;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(74, 86, 96);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 48, 55);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(53, 63, 72);
        button.BackColor = role switch
        {
            ButtonRole.Primary => Color.FromArgb(58, 88, 122),
            ButtonRole.Success => Color.FromArgb(38, 132, 89),
            ButtonRole.Warning => Color.FromArgb(151, 112, 42),
            ButtonRole.Danger => Color.FromArgb(151, 64, 58),
            _ => Color.FromArgb(52, 64, 78),
        };
        button.ForeColor = Color.FromArgb(238, 243, 246);
        button.Margin = new Padding(0, 4, 8, 4);
        button.UseVisualStyleBackColor = false;
    }

    private static void ConfigureTextBox(TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 5, 8, 5);
        textBox.BackColor = Color.FromArgb(18, 21, 24);
        textBox.ForeColor = Color.FromArgb(233, 238, 241);
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    private static void ConfigureComboBox(ComboBox comboBox)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.BackColor = Color.FromArgb(18, 21, 24);
        comboBox.ForeColor = Color.FromArgb(233, 238, 241);
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.Margin = new Padding(0, 8, 12, 0);
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = Color.FromArgb(18, 21, 24);
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Color.FromArgb(48, 55, 62);
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 42, 49);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(235, 241, 244);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 42, 49);
        grid.DefaultCellStyle.BackColor = Color.FromArgb(22, 26, 30);
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(231, 236, 239);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(58, 89, 118);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(26, 30, 35);
        grid.ColumnHeadersHeight = 30;
        grid.RowTemplate.Height = 28;
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(string name, string header, int width = 120, bool fill = false)
    {
        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            Width = width,
            ReadOnly = true,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
    }

    private static void ApplyPlaylistRowStyle(DataGridViewRow row, string status, bool dark)
    {
        row.DefaultCellStyle.BackColor = (status, dark) switch
        {
            ("PLAYING", true) => Color.FromArgb(32, 113, 73),
            ("NEXT", true) => Color.FromArgb(78, 69, 38),
            ("MISSING", true) => Color.FromArgb(78, 41, 35),
            ("SKIPPED", true) => Color.FromArgb(45, 49, 53),
            ("PLAYING", false) => Color.FromArgb(204, 239, 219),
            ("NEXT", false) => Color.FromArgb(255, 242, 204),
            ("MISSING", false) => Color.FromArgb(255, 224, 210),
            ("SKIPPED", false) => Color.FromArgb(226, 230, 234),
            _ => dark
                ? row.Index % 2 == 0 ? Color.FromArgb(22, 26, 30) : Color.FromArgb(26, 30, 35)
                : row.Index % 2 == 0 ? Color.White : Color.FromArgb(240, 244, 247),
        };
        row.DefaultCellStyle.ForeColor = status == "PLAYING" && dark ? Color.White : dark ? Color.FromArgb(231, 236, 239) : Color.FromArgb(25, 31, 36);
        row.DefaultCellStyle.SelectionBackColor = dark ? Color.FromArgb(58, 89, 118) : Color.FromArgb(58, 121, 184);
        row.DefaultCellStyle.SelectionForeColor = Color.White;
    }

    private static bool HasSubdirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsAudioFile(string path)
    {
        return AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    }

    private static string ReadDurationText(string path)
    {
        return TryReadDuration(path, out var duration) ? FormatTime(duration) : "--";
    }

    private static bool TryReadDuration(string path, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            duration = reader.TotalTime;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static AudioMetadata ReadAudioMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return new AudioMetadata("--", "--");
        }

        try
        {
            using var reader = new AudioFileReader(path);
            return new AudioMetadata(FormatTime(reader.TotalTime), reader.WaveFormat.Channels.ToString());
        }
        catch
        {
            return new AudioMetadata("--", "--");
        }
    }

    private static string FormatFileSize(string path)
    {
        try
        {
            var bytes = new FileInfo(path).Length;
            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:0.##} KB";
            }

            return $"{bytes / 1024.0 / 1024.0:0.##} MB";
        }
        catch
        {
            return "--";
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        var totalMinutes = (int)time.TotalMinutes;
        return $"{totalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private static string FormatPlaylistTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
    }

    private static bool TryParsePlaylistTime(string? text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        if (double.TryParse(normalized, out var seconds))
        {
            time = TimeSpan.FromSeconds(seconds);
            return time >= TimeSpan.Zero;
        }

        var parts = normalized.Split(':');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        var hourText = parts.Length == 3 ? parts[0] : "0";
        var minuteText = parts.Length == 3 ? parts[1] : parts[0];
        var secondText = parts.Length == 3 ? parts[2] : parts[1];
        if (!int.TryParse(hourText, out var hours) ||
            !int.TryParse(minuteText, out var minutes) ||
            !double.TryParse(secondText, out var parsedSeconds) ||
            hours < 0 ||
            minutes < 0 ||
            parsedSeconds < 0)
        {
            return false;
        }

        time = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(parsedSeconds);
        return true;
    }

    private sealed class PlaylistItem(string path)
    {
        public string Path { get; } = path;

        public bool PlayEnabled { get; set; } = true;

        public TimeSpan? StartTimeOverride { get; set; }
    }

    private sealed class PlaylistFileItem
    {
        public string Path { get; set; } = "";

        public bool PlayEnabled { get; set; } = true;

        public string? StartTime { get; set; }
    }

    private sealed class AppSettings
    {
        public string? MediaRoot { get; set; }

        public string? SelectedFolder { get; set; }

        public string? AudioDeviceId { get; set; }

        public float Volume { get; set; } = 1f;

        public bool DarkMode { get; set; } = true;

        public int SelectedPlaylistIndex { get; set; } = -1;

        public List<PlaylistFileItem> Playlist { get; set; } = [];
    }

    private sealed record AudioMetadata(string Duration, string Channels);

    private sealed record AudioDeviceItem(string? DeviceId, string Name)
    {
        public override string ToString()
        {
            return Name;
        }
    }

    private enum ButtonRole
    {
        Neutral,
        Primary,
        Success,
        Warning,
        Danger,
    }

    private static class WaveformAnalyzer
    {
        public static float[] CreatePeaks(string path, int targetPeaks)
        {
            using var reader = new AudioFileReader(path);
            var channels = Math.Max(1, reader.WaveFormat.Channels);
            var totalSamples = Math.Max(1L, reader.Length / sizeof(float));
            var frames = Math.Max(1L, totalSamples / channels);
            var framesPerPeak = Math.Max(1, (int)(frames / Math.Max(1, targetPeaks)));
            var peaks = new List<float>(targetPeaks);
            var buffer = new float[framesPerPeak * channels];

            while (true)
            {
                var read = reader.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                var peak = 0f;
                for (var i = 0; i < read; i++)
                {
                    peak = Math.Max(peak, Math.Abs(buffer[i]));
                }

                peaks.Add(Math.Clamp(peak, 0f, 1f));
            }

            return Normalize(peaks);
        }

        private static float[] Normalize(List<float> peaks)
        {
            if (peaks.Count == 0)
            {
                return [];
            }

            var max = Math.Max(0.001f, peaks.Max());
            var normalized = new float[peaks.Count];
            for (var i = 0; i < peaks.Count; i++)
            {
                normalized[i] = MathF.Sqrt(peaks[i] / max);
            }

            return normalized;
        }
    }
}
