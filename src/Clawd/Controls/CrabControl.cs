using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Clawd.Controls;

public class CrabControl : Control
{
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();

    // Screen-space position of the crab (left edge of body)
    private double _screenX;
    private double _speed = 0.5;
    private int _direction = 1;
    private bool _initialized;

    // The screen work area the crab can roam
    public Rect ScreenWorkArea { get; set; }

    // Window dimensions — generous padding for hat, particles, text, friend
    // Layout: [friendPad][crab][friendPad] horizontally, [textPad][hat+crab+legs] vertically
    private const int PixelSize = 3;
    private const int GridW = 24;
    private const int GridH = 19;
    private const int Pad = 80;        // horizontal padding each side (friend, particles)
    private const int TopPad = 60;     // above crab (hat, floating text, particles)
    private const int BottomPad = 10;  // below legs

    private static readonly int CrabW = GridW * PixelSize;  // 72
    private static readonly int CrabH = GridH * PixelSize;  // 57

    public static double WindowWidth => CrabW + Pad * 2;           // 232
    public static double WindowHeight => CrabH + TopPad + BottomPad; // 127

    // Where the crab body draws inside the window (local coords)
    private double CrabLocalX => Pad;
    private double CrabLocalY => TopPad;

    // Events to move the host window / open chat
    public event Action<double, double>? RequestWindowMove;
    public event Action<double, double>? RequestOpenChat; // screen X, screen Y of crab
    public event Action? RequestSummarizeClipboard;

    // Chat open — freeze the crab in place
    private bool _chatOpen;
    public void SetChatOpen(bool open) { _chatOpen = open; _mood.OnInteraction(); }

    // Weather
    private Services.WeatherInfo? _weatherInfo;
    public void SetWeather(Services.WeatherInfo? info) => _weatherInfo = info;

    // Mood
    private readonly Services.MoodService _mood = new();
    public Services.MoodService Mood => _mood;

    // Animation
    private int _tickCount;
    private int _frame;

    // State machine
    private CrabState _state = CrabState.Walking;
    private int _stateTicks;
    private int _stateDuration;

    // Jump
    private double _jumpY;
    private double _jumpVelocity;

    // Sleep
    private double _zzzPhase;

    // Detective
    private int _detectiveLookDir;
    private int _detectiveLookTicks;
    private bool _detectiveLookedBoth;

    // Claw animation
    private int _clawOffset;

    // Pointer
    private Point _pointerPos;
    private int _eatingTicks;
    private int _mouthFrame;
    private double _yumAlpha;
    private double _yumY;
    private bool _showYum;
    private double _excitedBounce;

    // Drag — in screen coords
    private bool _dragging;
    private double _dragOffsetX;
    private double _dragOffsetY;
    private double _screenY; // screen Y when dragging/falling (normally at bottom)

    private int _ouchTicks;
    private bool _atBottom = true;

    // Double-click -> pet
    private DateTime _lastClickTime;
    private int _petTicks;

    // Bored (no interaction)
    private DateTime _lastInteractionTime = DateTime.UtcNow;
    private int _boredYawnPhase;

    // Tripping
    private int _tripPhase;
    private int _tripTicks;

    // Celebration / scared
    private int _celebrationSpins;
    private double _scaredShake;

    // Window surfing
    private int _surfTicks;

    // Chasing mouse
    private double _mouseScreenX;

    // Inspecting
    private int _readNods;
    private bool _readApproves;

    // Dancing
    private int _danceMoves;

    // Friday dance
    private bool _isFriday;
    private int _fridayDanceCooldown;

    // Hats
    private HatType _hat;

    // Particles (local coords relative to window)
    private readonly List<Particle> _particles = new();

    // Friend crab — offset from main crab screen X
    private double _friendOffsetX = -40;
    private double _friendSpeed = 0.3;
    private int _friendFrame;
    private int _friendDir = 1;
    private bool _friendEnabled = true;

    // Context menu
    private ContextMenu? _contextMenu;

    private enum CrabState
    {
        Walking, Idle, Jumping, Sleeping, Detective, Excited, Eating,
        Dancing, Celebrating, Scared, Bored, Tripping, ChasingMouse,
        WindowSurfing, Inspecting, BeingPetted, FridayDance, Exploring
    }

    private enum HatType { None, Cowboy, TopHat, PartyHat, Crown, Beret }
    private enum ParticleType { Footprint, Bubble, Heart, Star, Confetti }

    private record struct Particle(
        double X, double Y, double VX, double VY,
        double Age, double MaxAge, ParticleType Type, double Size);

    #region Colors

    private static readonly IBrush Body = new SolidColorBrush(Color.Parse("#D77757"));
    private static readonly IBrush Eye = new SolidColorBrush(Color.Parse("#000000"));
    private static readonly IBrush Mouth = new SolidColorBrush(Color.Parse("#8B3A2A"));
    private static readonly IBrush HatBrown = new SolidColorBrush(Color.Parse("#8B5E3C"));
    private static readonly IBrush HatDark = new SolidColorBrush(Color.Parse("#2D2D2D"));
    private static readonly IBrush HatPink = new SolidColorBrush(Color.Parse("#D946EF"));
    private static readonly IBrush HatGold = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush HatRed = new SolidColorBrush(Color.Parse("#EF4444"));

    #endregion

    #region Sprites

    private static readonly byte[,] BodyBase =
    {
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
        { 0,0,0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0,0,0 },
    };

    private static readonly byte[,] BodyFallen =
    {
        { 0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,0,0 },
    };

    private static readonly byte[,] ClawSprite = { { 1,1,1,1 }, { 1,1,1,1 }, { 1,1,1,1 }, { 1,1,1,1 } };

    private static readonly (int c, int r)[] EyesNormal = { (7,3),(8,4),(7,5), (16,3),(15,4),(16,5) };
    private static readonly (int c, int r)[] EyesRight = { (8,3),(9,4),(8,5), (17,3),(16,4),(17,5) };
    private static readonly (int c, int r)[] EyesLeft = { (6,3),(7,4),(6,5), (15,3),(14,4),(15,5) };
    private static readonly (int c, int r)[] EyesClosed = { (6,4),(7,4),(8,4), (15,4),(16,4),(17,4) };
    private static readonly (int c, int r)[] EyesWide = { (7,3),(8,3),(7,4),(8,4),(7,5),(8,5), (15,3),(16,3),(15,4),(16,4),(15,5),(16,5) };
    private static readonly (int c, int r)[] EyesHappy = { (6,5),(7,4),(8,5), (15,5),(16,4),(17,5) };
    private static readonly (int c, int r)[] EyesDizzy = { (6,3),(8,5),(7,4), (17,3),(15,5),(16,4) };

    private static readonly (int c, int r)[] LegsA =
    {
        (5,14),(6,14),(5,15),(6,15),(5,16),(6,16),(5,17),(6,17),(5,18),(6,18),
        (8,14),(9,14),(8,15),(9,15),(8,16),(9,16),(8,17),(9,17),(8,18),(9,18),
        (14,14),(15,14),(14,15),(15,15),(14,16),(15,16),(14,17),(15,17),(14,18),(15,18),
        (17,14),(18,14),(17,15),(18,15),(17,16),(18,16),(17,17),(18,17),(17,18),(18,18),
    };
    private static readonly (int c, int r)[] LegsB =
    {
        (4,14),(5,14),(4,15),(5,15),(4,16),(5,16),(4,17),(5,17),(4,18),(5,18),
        (9,14),(10,14),(9,15),(10,15),(9,16),(10,16),(9,17),(10,17),(9,18),(10,18),
        (13,14),(14,14),(13,15),(14,15),(13,16),(14,16),(13,17),(14,17),(13,18),(14,18),
        (18,14),(19,14),(18,15),(19,15),(18,16),(19,16),(18,17),(19,17),(18,18),(19,18),
    };
    private static readonly (int c, int r)[] LegsTucked =
    {
        (6,14),(7,14),(6,15),(7,15), (10,14),(11,14),(10,15),(11,15),
        (12,14),(13,14),(12,15),(13,15), (16,14),(17,14),(16,15),(17,15),
    };

    private static readonly (int c, int r)[] MouthOpen = { (10,7),(11,7),(12,7),(13,7), (9,8),(10,8),(11,8),(12,8),(13,8),(14,8), (10,9),(11,9),(12,9),(13,9) };
    private static readonly (int c, int r)[] MouthSmall = { (10,8),(11,8),(12,8),(13,8) };

    private static readonly (int dx, int dy)[] PxZ = { (0,0),(1,0),(2,0), (1,1), (0,2),(1,2),(2,2) };
    private static readonly (int dx, int dy)[] PxHeart = { (1,0),(3,0), (0,1),(1,1),(2,1),(3,1),(4,1), (0,2),(1,2),(2,2),(3,2),(4,2), (1,3),(2,3),(3,3), (2,4) };
    private static readonly (int dx, int dy)[] PxStar = { (2,0), (1,1),(2,1),(3,1), (0,2),(1,2),(2,2),(3,2),(4,2), (1,3),(3,3), (0,4),(4,4) };

    private static readonly byte[,] HatCowboy =
    {
        { 0,0,0,0,1,1,1,1,0,0,0,0 },
        { 0,0,0,1,1,1,1,1,1,0,0,0 },
        { 1,1,1,1,1,1,1,1,1,1,1,1 },
        { 0,1,1,1,1,1,1,1,1,1,1,0 },
    };
    private static readonly byte[,] HatTop =
    {
        { 0,0,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,0,0 },
        { 1,1,1,1,1,1,1,1 },
    };
    private static readonly byte[,] HatParty =
    {
        { 0,0,0,1,0,0 },
        { 0,0,1,1,0,0 },
        { 0,0,1,1,0,0 },
        { 0,1,1,1,1,0 },
        { 1,1,1,1,1,1 },
    };
    private static readonly byte[,] HatCrown =
    {
        { 0,1,0,1,0,0,1,0,1,0 },
        { 1,1,1,1,1,1,1,1,1,1 },
        { 0,1,1,1,1,1,1,1,1,0 },
    };
    private static readonly byte[,] HatBeret =
    {
        { 0,0,0,1,1,1,1,1,0,0 },
        { 0,0,1,1,1,1,1,1,1,0 },
        { 0,1,1,1,1,1,1,1,0,0 },
    };

    private static readonly byte[,] FriendBody =
    {
        { 0,0,1,1,1,1,1,1,1,1,0,0 },
        { 1,1,1,1,1,1,1,1,1,1,1,1 },
        { 1,1,1,2,1,1,1,1,2,1,1,1 },
        { 1,1,1,1,1,1,1,1,1,1,1,1 },
        { 0,0,1,1,1,1,1,1,1,1,0,0 },
        { 0,0,1,0,1,0,0,1,0,1,0,0 },
        { 0,1,0,0,0,0,0,0,0,0,1,0 },
    };

    #endregion

    public CrabControl()
    {
        _stateDuration = 200 + _rng.Next(300);
        _isFriday = DateTime.Now.DayOfWeek == DayOfWeek.Friday;

        var hats = Enum.GetValues<HatType>();
        _hat = hats[_rng.Next(hats.Length)];

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    #region Public API

    public void DoFeed() { _mood.OnFed(); EnterState(CrabState.Eating); }

    public void DoPet()
    {
        _mood.OnPetted();
        EnterState(CrabState.BeingPetted);
        for (var i = 0; i < 5; i++) SpawnParticle(ParticleType.Heart);
    }

    public void DoDance() => EnterState(CrabState.Dancing);

    public void DoChangeHat()
    {
        var hats = Enum.GetValues<HatType>();
        _hat = hats[_rng.Next(hats.Length)];
    }

    public void DoToggleFriend() => _friendEnabled = !_friendEnabled;

    #endregion

    #region Input handlers

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        // Right-click -> context menu
        if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            ShowContextMenu(pos);
            return;
        }

        if (!props.IsLeftButtonPressed) return;

        // Double-click -> pet
        var now = DateTime.UtcNow;
        if ((now - _lastClickTime).TotalMilliseconds < 350)
        {
            e.Handled = true;
            EnterState(CrabState.BeingPetted);
            for (var i = 0; i < 5; i++) SpawnParticle(ParticleType.Heart);
            _lastClickTime = DateTime.MinValue;
            return;
        }
        _lastClickTime = now;

        // Start drag
        e.Handled = true;
        _dragging = true;
        // Store offset from pointer to crab screen position
        _dragOffsetX = pos.X - CrabLocalX;
        if (_atBottom)
            _screenY = ScreenWorkArea.Bottom - CrabH - 4;
        _atBottom = false;
        _dragOffsetY = pos.Y - CrabLocalY;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerPos = e.GetPosition(this);
        _lastInteractionTime = DateTime.UtcNow;

        if (_dragging)
        {
            // Move crab in screen space based on local pointer + window position
            var win = TopLevel.GetTopLevel(this) as Window;
            if (win == null) return;

            var scaling = win.Screens.Primary?.Scaling ?? 1.0;
            var winScreenX = win.Position.X / scaling;
            var winScreenY = win.Position.Y / scaling;

            _screenX = winScreenX + _pointerPos.X - _dragOffsetX - Pad;
            _screenY = winScreenY + _pointerPos.Y - _dragOffsetY - TopPad;

            MoveWindowToCrab();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;

        var bottomY = ScreenWorkArea.Bottom - CrabH - 4;

        if (_screenY >= bottomY - 5)
        {
            // Dropped near the bottom — snap to bottom and walk
            _atBottom = true;
        }
        else
        {
            // Dropped somewhere on screen — stay there and peek
            _atBottom = false;
            EnterState(CrabState.Exploring);
        }
    }

    #endregion

    #region Context menu

    private void ShowContextMenu(Point pos)
    {
        _contextMenu?.Close();
        _contextMenu = new ContextMenu();

        var feed = new MenuItem { Header = "Feed Clawd \U0001F355" };
        feed.Click += (_, _) => EnterState(CrabState.Eating);

        var pet = new MenuItem { Header = "Pet Clawd \U0001F495" };
        pet.Click += (_, _) => { EnterState(CrabState.BeingPetted); for (var i = 0; i < 5; i++) SpawnParticle(ParticleType.Heart); };

        var dance = new MenuItem { Header = "Make Clawd dance \U0001F483" };
        dance.Click += (_, _) => EnterState(CrabState.Dancing);

        var hatItem = new MenuItem { Header = "Change hat \U0001F3A9" };
        hatItem.Click += (_, _) => DoChangeHat();

        var friendItem = new MenuItem { Header = _friendEnabled ? "Dismiss friend \U0001F44B" : "Call friend \U0001F40B" };
        friendItem.Click += (_, _) => _friendEnabled = !_friendEnabled;

        var chatItem = new MenuItem { Header = "Chat with Clawd \U0001F4AC" };
        chatItem.Click += (_, _) => RequestOpenChat?.Invoke(_screenX, _atBottom ? ScreenWorkArea.Bottom - CrabH - 4 : _screenY);

        var clipItem = new MenuItem { Header = "Summarize clipboard \U0001F4CB" };
        clipItem.Click += (_, _) => RequestSummarizeClipboard?.Invoke();

        _contextMenu.Items.Add(chatItem);
        _contextMenu.Items.Add(clipItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(feed);
        _contextMenu.Items.Add(pet);
        _contextMenu.Items.Add(dance);
        _contextMenu.Items.Add(hatItem);
        _contextMenu.Items.Add(friendItem);

        // Status
        _contextMenu.Items.Add(new Separator());
        var moodItem = new MenuItem
        {
            Header = $"Mood: {_mood.MoodText} {_mood.MoodEmoji}",
            IsEnabled = false
        };
        _contextMenu.Items.Add(moodItem);

        if (_weatherInfo != null)
        {
            var weatherItem = new MenuItem
            {
                Header = $"Weather: {_weatherInfo.Description}, {_weatherInfo.Temperature:0}°C",
                IsEnabled = false
            };
            _contextMenu.Items.Add(weatherItem);
        }

        _contextMenu.Open(this);
    }

    #endregion

    #region Window positioning

    /// <summary>Move the host window so the crab appears at _screenX on screen.</summary>
    private void MoveWindowToCrab()
    {
        double winX, winY;

        if (_atBottom)
        {
            winX = _screenX - Pad;
            winY = ScreenWorkArea.Bottom - WindowHeight;
        }
        else
        {
            winX = _screenX - Pad;
            winY = _screenY - TopPad;
        }

        RequestWindowMove?.Invoke(winX, winY);
    }

    #endregion

    #region Tick / state machine

    private void OnTick(object? sender, EventArgs e)
    {
        if (ScreenWorkArea.Width < 10) return; // not ready yet

        _tickCount++;

        // Update mood every ~5 seconds
        if (_tickCount % 300 == 0) _mood.Update();

        if (!_initialized)
        {
            _screenX = ScreenWorkArea.X + ScreenWorkArea.Width * 0.3 + _rng.NextDouble() * ScreenWorkArea.Width * 0.4;
            _screenY = ScreenWorkArea.Bottom - CrabH - 4;
            _initialized = true;
        }

        // Yum text fade
        if (_showYum)
        {
            _yumAlpha -= 0.008;
            _yumY -= 0.5;
            if (_yumAlpha <= 0) { _showYum = false; _yumAlpha = 0; }
        }

        // Particles
        UpdateParticles();

        // When chat is open, freeze — only update particles and render
        if (!_chatOpen)
        {
            _stateTicks++;

            // Random bubbles
            if (_tickCount % 200 == 0 && _rng.NextDouble() < 0.3)
                SpawnParticle(ParticleType.Bubble);

            // Bored check
            if (_state is CrabState.Walking or CrabState.Idle &&
                (DateTime.UtcNow - _lastInteractionTime).TotalSeconds > 30)
                EnterState(CrabState.Bored);

            // Friday dance
            if (_isFriday && _fridayDanceCooldown <= 0 &&
                _state is CrabState.Walking or CrabState.Idle && _rng.NextDouble() < 0.001)
            {
                EnterState(CrabState.FridayDance);
                _fridayDanceCooldown = 600;
            }
            if (_fridayDanceCooldown > 0) _fridayDanceCooldown--;

            // Random trip
            if (_state == CrabState.Walking && _rng.NextDouble() < 0.0003)
                EnterState(CrabState.Tripping);

            // Random mouse chase
            if (_state == CrabState.Idle && _rng.NextDouble() < 0.003)
                EnterState(CrabState.ChasingMouse);

            // Random inspecting
            if (_state == CrabState.Idle && _rng.NextDouble() < 0.002)
                EnterState(CrabState.Inspecting);

            // Random exploring
            if (_state == CrabState.Idle && _rng.NextDouble() < 0.001)
                EnterState(CrabState.Exploring);

            // Random window surfing
            if (_state is CrabState.Walking or CrabState.Idle && _rng.NextDouble() < 0.0005)
                EnterState(CrabState.WindowSurfing);

            // Friend crab
            UpdateFriendCrab();

            if (_ouchTicks > 0) _ouchTicks--;

            // State update
            switch (_state)
            {
                case CrabState.Walking: UpdateWalking(); break;
                case CrabState.Idle: UpdateIdle(); break;
                case CrabState.Jumping: UpdateJumping(); break;
                case CrabState.Sleeping: UpdateSleeping(); break;
                case CrabState.Detective: UpdateDetective(); break;
                case CrabState.Excited: UpdateExcited(); break;
                case CrabState.Eating: UpdateEating(); break;
                case CrabState.Dancing: UpdateDancing(); break;
                case CrabState.Celebrating: UpdateCelebrating(); break;
                case CrabState.Scared: UpdateScared(); break;
                case CrabState.Bored: UpdateBored(); break;
                case CrabState.Tripping: UpdateTripping(); break;
                case CrabState.ChasingMouse: UpdateChasingMouse(); break;
                case CrabState.WindowSurfing: UpdateWindowSurfing(); break;
                case CrabState.Inspecting: UpdateInspecting(); break;
                case CrabState.BeingPetted: UpdateBeingPetted(); break;
                case CrabState.FridayDance: UpdateFridayDance(); break;
                case CrabState.Exploring: UpdateExploring(); break;
            }

            // Move the window to follow the crab
            if (!_dragging)
                MoveWindowToCrab();
        }

        InvalidateVisual();
    }

    private void EnterState(CrabState s)
    {
        _state = s;
        _stateTicks = 0;
        _lastInteractionTime = DateTime.UtcNow;
        switch (s)
        {
            case CrabState.Walking:
                _stateDuration = 120 + _rng.Next(360);
                _speed = 0.3 + _rng.NextDouble() * 0.5;
                _clawOffset = 0;
                break;
            case CrabState.Idle:
                _stateDuration = 60 + _rng.Next(120);
                break;
            case CrabState.Jumping:
                _jumpY = 0;
                _jumpVelocity = -3.5 - _rng.NextDouble() * 1.5;
                break;
            case CrabState.Sleeping:
                _stateDuration = 300 + _rng.Next(400);
                _zzzPhase = 0;
                break;
            case CrabState.Detective:
                _detectiveLookDir = _rng.NextDouble() < 0.5 ? -1 : 1;
                _detectiveLookTicks = 0;
                _detectiveLookedBoth = false;
                _stateDuration = 180 + _rng.Next(120);
                break;
            case CrabState.Excited:
                _excitedBounce = 0;
                break;
            case CrabState.Eating:
                _eatingTicks = 0;
                _mouthFrame = 0;
                _showYum = true;
                _yumAlpha = 1.0;
                _yumY = 0;
                break;
            case CrabState.Dancing:
                _danceMoves = 0;
                _stateDuration = 180;
                break;
            case CrabState.Celebrating:
                _celebrationSpins = 0;
                _stateDuration = 120;
                break;
            case CrabState.Scared:
                _scaredShake = 0;
                _stateDuration = 120;
                break;
            case CrabState.Bored:
                _boredYawnPhase = 0;
                _stateDuration = 300;
                break;
            case CrabState.Tripping:
                _tripPhase = 0;
                _tripTicks = 0;
                _stateDuration = 120;
                break;
            case CrabState.ChasingMouse:
                _mouseScreenX = _screenX + 100 * _direction; // chase a spot ahead
                _stateDuration = 200;
                break;
            case CrabState.WindowSurfing:
                _surfTicks = 0;
                _stateDuration = 60;
                break;
            case CrabState.Inspecting:
                _readNods = 0;
                _readApproves = _rng.NextDouble() < 0.7;
                _stateDuration = 120;
                break;
            case CrabState.BeingPetted:
                _petTicks = 0;
                _stateDuration = 90;
                break;
            case CrabState.FridayDance:
                _danceMoves = 0;
                _stateDuration = 240;
                break;
            case CrabState.Exploring:
                _stateDuration = 180 + _rng.Next(120);
                break;
        }

    }

    #region Update methods

    private void UpdateWalking()
    {
        if (_tickCount % 10 == 0) _frame = (_frame + 1) % 2;
        if (_tickCount % 20 == 0) _clawOffset = _clawOffset == 0 ? -1 : 0;
        _screenX += _speed * _direction;
        BounceOffEdges();

        if (_stateTicks >= _stateDuration)
        {
            var r = _rng.NextDouble();
            if (r < 0.25) EnterState(CrabState.Idle);
            else if (r < 0.35) EnterState(CrabState.Jumping);
            else if (r < 0.45) EnterState(CrabState.Detective);
            else if (r < 0.52) { _direction = -_direction; EnterState(CrabState.Walking); }
            else EnterState(CrabState.Walking);
        }
    }

    private void UpdateIdle()
    {
        if (_tickCount % 30 == 0)
            _clawOffset = _clawOffset switch { -1 => 0, 0 => 1, 1 => -1, _ => 0 };

        if (_stateTicks >= _stateDuration)
        {
            var r = _rng.NextDouble();
            if (r < 0.12) EnterState(CrabState.Sleeping);
            else if (r < 0.25) EnterState(CrabState.Detective);
            else if (r < 0.40) EnterState(CrabState.Jumping);
            else { if (_rng.NextDouble() < 0.4) _direction = -_direction; EnterState(CrabState.Walking); }
        }
    }

    private void UpdateJumping()
    {
        _jumpVelocity += 0.18;
        _jumpY += _jumpVelocity;
        _clawOffset = -2;
        _screenX += _speed * _direction * 0.5;
        BounceOffEdges();
        if (_jumpY >= 0) { _jumpY = 0; _clawOffset = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateSleeping()
    {
        _zzzPhase += 0.02;
        _clawOffset = 1;
        if (_stateTicks >= _stateDuration) { _clawOffset = 0; EnterState(CrabState.Jumping); }
    }

    private void UpdateDetective()
    {
        _detectiveLookTicks++;
        if (_detectiveLookTicks > 50 && !_detectiveLookedBoth)
        {
            _detectiveLookDir = -_detectiveLookDir;
            _detectiveLookTicks = 0;
            _detectiveLookedBoth = true;
        }
        _clawOffset = _tickCount % 40 < 20 ? -1 : -2;
        if (_stateTicks >= _stateDuration) { _clawOffset = 0; _direction = _detectiveLookDir; EnterState(CrabState.Walking); }
    }

    private void UpdateExcited()
    {
        _excitedBounce = Math.Sin(_stateTicks * 0.3) * 3;
        _clawOffset = _tickCount % 8 < 4 ? -2 : -1;
        if (_stateTicks > 300) EnterState(CrabState.Idle);
    }

    private void UpdateEating()
    {
        _eatingTicks++;
        if (_eatingTicks % 6 == 0) _mouthFrame = (_mouthFrame + 1) % 2;
        _clawOffset = _eatingTicks % 12 < 6 ? 0 : 1;
        if (_eatingTicks > 120) EnterState(CrabState.Jumping);
    }

    private void UpdateDancing()
    {
        _screenX += Math.Sin(_stateTicks * 0.15) * 1.5;
        _clawOffset = _tickCount % 12 < 6 ? -2 : 0;
        if (_tickCount % 8 == 0) _frame = (_frame + 1) % 2;
        _danceMoves++;
        BounceOffEdges();
        if (_stateTicks >= _stateDuration) { _clawOffset = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateCelebrating()
    {
        if (_tickCount % 10 == 0) { _direction = -_direction; _celebrationSpins++; }
        _clawOffset = -2;
        _jumpY = Math.Sin(_stateTicks * 0.15) * -15;
        if (_stateTicks >= _stateDuration) { _jumpY = 0; _clawOffset = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateScared()
    {
        _scaredShake = Math.Sin(_stateTicks * 1.5) * 2;
        _clawOffset = -2;
        _screenX -= _direction * 0.3;
        BounceOffEdges();
        if (_stateTicks >= _stateDuration) { _clawOffset = 0; EnterState(CrabState.Idle); }
    }

    private void UpdateBored()
    {
        _boredYawnPhase = (_stateTicks / 40) % 4;
        _clawOffset = _boredYawnPhase == 1 ? -1 : 0;
        if (_tickCount % 20 == 0) _frame = (_frame + 1) % 2;
        if (_stateTicks >= _stateDuration) EnterState(CrabState.Sleeping);
    }

    private void UpdateTripping()
    {
        _tripTicks++;
        if (_tripPhase == 0)
        {
            _screenX += _direction * 1.5;
            if (_tripTicks > 15) { _tripPhase = 1; _tripTicks = 0; }
        }
        else if (_tripPhase == 1)
        {
            if (_tripTicks > 60) { _tripPhase = 2; _tripTicks = 0; }
        }
        else
        {
            if (_tripTicks > 30) EnterState(CrabState.Idle);
        }
        BounceOffEdges();
    }

    private void UpdateChasingMouse()
    {
        var dx = _mouseScreenX - _screenX;
        if (Math.Abs(dx) > 5)
        {
            _direction = dx > 0 ? 1 : -1;
            _screenX += _direction * 0.8;
            if (_tickCount % 8 == 0) _frame = (_frame + 1) % 2;
            if (_tickCount % 16 == 0) _clawOffset = _clawOffset == 0 ? -1 : 0;
        }
        BounceOffEdges();
        if (_stateTicks >= _stateDuration) EnterState(CrabState.Idle);
    }

    private void UpdateWindowSurfing()
    {
        _clawOffset = 1;
        _screenX += _rng.NextDouble() * 4 - 2;
        _surfTicks++;
        BounceOffEdges();
        if (_surfTicks > _stateDuration) { _clawOffset = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateInspecting()
    {
        _clawOffset = -1;
        if (_stateTicks > 60 && _stateTicks % 20 == 0)
        {
            _readNods++;
            if (_readApproves)
                _jumpY = Math.Sin(_readNods * 1.5) * -3;
            else
                _screenX += Math.Sin(_readNods * 2) * 3;
        }
        if (_stateTicks >= _stateDuration)
        {
            _jumpY = 0;
            _clawOffset = 0;
            if (_readApproves) { SpawnParticle(ParticleType.Star); EnterState(CrabState.Walking); }
            else EnterState(CrabState.Idle);
        }
    }

    private void UpdateBeingPetted()
    {
        _petTicks++;
        _clawOffset = 0;
        _jumpY = Math.Sin(_petTicks * 0.2) * -2;
        if (_petTicks % 20 == 0) SpawnParticle(ParticleType.Heart);
        if (_petTicks >= _stateDuration) { _jumpY = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateFridayDance()
    {
        _screenX += Math.Sin(_stateTicks * 0.2) * 2;
        _clawOffset = _tickCount % 6 < 3 ? -2 : 1;
        _jumpY = Math.Sin(_stateTicks * 0.3) * -8;
        if (_tickCount % 6 == 0) _frame = (_frame + 1) % 2;
        if (_tickCount % 4 == 0) _direction = -_direction;
        BounceOffEdges();
        if (_stateTicks % 15 == 0) SpawnParticle(ParticleType.Confetti);
        if (_stateTicks >= _stateDuration) { _jumpY = 0; _clawOffset = 0; EnterState(CrabState.Walking); }
    }

    private void UpdateExploring()
    {
        // Stay at current Y, look around curiously
        if (_tickCount % 60 == 0) _clawOffset = _clawOffset == -1 ? 0 : -1;
        if (_tickCount % 90 == 0 && _rng.NextDouble() < 0.3) _direction = -_direction;

        if (_stateTicks >= _stateDuration)
        {
            _clawOffset = 0;
            // Return to bottom and resume walking
            _atBottom = true;
            _screenY = ScreenWorkArea.Bottom - CrabH - 4;
            EnterState(CrabState.Walking);
        }
    }

    private void BounceOffEdges()
    {
        var minX = ScreenWorkArea.X + 10;
        var maxX = ScreenWorkArea.Right - CrabW - 10;
        if (_screenX > maxX) { _screenX = maxX; _direction = -1; }
        else if (_screenX < minX) { _screenX = minX; _direction = 1; }
    }

    #endregion

    #endregion

    #region Particles

    private void SpawnParticle(ParticleType type)
    {
        var cx = CrabLocalX + 12 * PixelSize;
        var cy = CrabLocalY - 5;
        var p = type switch
        {
            ParticleType.Heart => new Particle(
                cx + _rng.Next(-10, 10), cy,
                _rng.NextDouble() - 0.5, -1.2 - _rng.NextDouble() * 0.8,
                0, 60 + _rng.Next(30), type, 2),
            ParticleType.Star => new Particle(
                cx + _rng.Next(-15, 15), cy - 5,
                _rng.NextDouble() * 2 - 1, -1.5 - _rng.NextDouble(),
                0, 50 + _rng.Next(20), type, 2),
            ParticleType.Bubble => new Particle(
                cx + _rng.Next(-5, 5), cy + 5,
                _rng.NextDouble() * 0.3 - 0.15, -0.5 - _rng.NextDouble() * 0.3,
                0, 80 + _rng.Next(40), type, 2 + _rng.Next(3)),
            ParticleType.Confetti => new Particle(
                cx + _rng.Next(-20, 20), cy - 10,
                _rng.NextDouble() * 3 - 1.5, -2 - _rng.NextDouble() * 2,
                0, 70 + _rng.Next(30), type, 2),
            _ => new Particle(cx, CrabLocalY + CrabH, 0, 0, 0, 120, type, 2),
        };
        _particles.Add(p);
    }

    private void UpdateParticles()
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Age++;
            p.X += p.VX;
            p.Y += p.VY;
            if (p.Type == ParticleType.Confetti) p.VY += 0.05;
            _particles[i] = p;
            if (p.Age >= p.MaxAge) _particles.RemoveAt(i);
        }
    }

    #endregion

    #region Friend crab

    private void UpdateFriendCrab()
    {
        if (!_friendEnabled) return;

        // Friend follows at an offset behind the main crab
        var targetOffset = -30.0 * _direction;
        var dx = targetOffset - _friendOffsetX;
        if (Math.Abs(dx) > 3)
        {
            _friendDir = dx > 0 ? 1 : -1;
            _friendOffsetX += _friendDir * _friendSpeed;
            if (_tickCount % 12 == 0) _friendFrame = (_friendFrame + 1) % 2;
        }

        // Clamp offset so friend stays in window bounds
        _friendOffsetX = Math.Clamp(_friendOffsetX, -Pad + 5, Pad + CrabW - 30);
    }

    #endregion

    #region Render

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (!_initialized) return;

        // All drawing uses local coordinates within the window
        var baseX = CrabLocalX + (_state == CrabState.Scared ? _scaredShake : 0);
        var baseY = CrabLocalY + _jumpY + (_state == CrabState.Excited ? _excitedBounce : 0);
        var flipH = _direction == -1;

        // Particles (behind crab)
        DrawParticles(ctx);

        // Friend crab
        if (_friendEnabled) DrawFriend(ctx);

        // Tripping: draw fallen body
        if (_state == CrabState.Tripping && _tripPhase == 1)
        {
            var fallenH = BodyFallen.GetLength(0) * PixelSize;
            var fallenY = CrabLocalY + CrabH - fallenH;
            DrawGrid(ctx, BodyFallen, baseX, fallenY, flipH, Body);
            DrawPixels(ctx, EyesDizzy, baseX, fallenY - 6 * PixelSize, flipH, Eye);
            DrawStateText(ctx, baseX, baseY, flipH);
            return;
        }

        // Hat
        DrawHat(ctx, baseX, baseY, flipH);

        // Body
        DrawGrid(ctx, BodyBase, baseX, baseY, flipH, Body);

        // Claws
        var clawY = baseY + (3 + _clawOffset) * PixelSize;
        DrawGrid(ctx, ClawSprite, flipH ? baseX + 20 * PixelSize : baseX, clawY, false, Body);
        DrawGrid(ctx, ClawSprite, flipH ? baseX : baseX + 20 * PixelSize, clawY, false, Body);

        // Eyes
        var eyes = _state switch
        {
            CrabState.Sleeping or CrabState.Bored when _boredYawnPhase is 1 or 2 => EyesClosed,
            CrabState.Detective => _detectiveLookDir > 0 ? EyesRight : EyesLeft,
            CrabState.Excited or CrabState.Scared => EyesWide,
            CrabState.Eating => _mouthFrame == 0 ? EyesHappy : EyesWide,
            CrabState.BeingPetted => EyesHappy,
            CrabState.ChasingMouse => _direction > 0 ? EyesRight : EyesLeft,
            CrabState.Exploring => EyesRight,
            CrabState.Tripping => EyesDizzy,
            CrabState.WindowSurfing => EyesWide,
            _ => _mood.Current is Services.Mood.Happy ? EyesHappy
               : _mood.Current is Services.Mood.Sad or Services.Mood.Lonely ? EyesClosed
               : EyesNormal
        };
        DrawPixels(ctx, eyes, baseX, baseY, flipH, Eye);

        // Mouth
        if (_state == CrabState.Eating)
            DrawPixels(ctx, _mouthFrame == 0 ? MouthOpen : MouthSmall, baseX, baseY, flipH, Mouth);
        else if (_state == CrabState.Bored && _boredYawnPhase is 1 or 2)
            DrawPixels(ctx, MouthOpen, baseX, baseY, flipH, Mouth);

        // Legs
        if (_state is CrabState.Jumping or CrabState.Celebrating)
            DrawPixels(ctx, LegsTucked, baseX, baseY, flipH, Body);
        else if (_state is CrabState.Sleeping or CrabState.BeingPetted)
            DrawPixels(ctx, LegsA, baseX, baseY, flipH, Body);
        else
            DrawPixels(ctx, _frame == 0 ? LegsA : LegsB, baseX, baseY, flipH, Body);

        // ZZZ
        if (_state == CrabState.Sleeping) DrawSleepZzz(ctx, baseX, baseY);

        // Yum bubble
        if (_showYum && _yumAlpha > 0) DrawYumBubble(ctx, baseX, baseY);

        // State-specific floating text
        DrawStateText(ctx, baseX, baseY, flipH);

        // Weather accessory (drawn on top)
        DrawWeatherAccessory(ctx, baseX, baseY, flipH);
    }

    private void DrawStateText(DrawingContext ctx, double baseX, double baseY, bool flipH)
    {
        if (_state == CrabState.Excited)
            DrawFloatingText(ctx, "!", baseX + 11 * PixelSize, baseY - 14, "#f0c674", 1);
        if (_state == CrabState.Celebrating)
            DrawFloatingText(ctx, "wooo!", baseX + 5 * PixelSize, baseY - 20, "#4ade80", 1);
        if (_state == CrabState.Scared)
            DrawFloatingText(ctx, "!!!", baseX + 9 * PixelSize, baseY - 14, "#ef4444", 1);
        if (_state == CrabState.Bored && _boredYawnPhase is 1 or 2)
            DrawFloatingText(ctx, "*yawn*", baseX + 5 * PixelSize, baseY - 16, "#8899aa", 0.7);
        if (_state == CrabState.Inspecting && _stateTicks > 60)
            DrawFloatingText(ctx, _readApproves ? "hmm, nice!" : "hmm...", baseX + 2 * PixelSize, baseY - 18, "#8899aa", 0.8);
        if (_state == CrabState.WindowSurfing)
            DrawFloatingText(ctx, "wheee!", baseX + 4 * PixelSize, baseY - 16, "#60a5fa", 1);
        if (_state == CrabState.FridayDance)
            DrawFloatingText(ctx, "It's Friday!", baseX - 2 * PixelSize, baseY - 22, "#f0c674", 1);
        if (_state == CrabState.BeingPetted)
            DrawFloatingText(ctx, "<3", baseX + 10 * PixelSize, baseY - 14, "#ef4444", 1);
        if (_state == CrabState.Exploring)
        {
            var texts = new[] { "hmm...", "interesting!", "ooh!", "what's this?", "exploring!" };
            DrawFloatingText(ctx, texts[(_stateTicks / 120) % texts.Length], baseX + 2 * PixelSize, baseY - 16, "#88bbdd", 0.9);
        }
        if (_ouchTicks > 0)
            DrawFloatingText(ctx, "ouch! don't do that, I am fragile!", baseX - 20, baseY - 20, "#ef4444", Math.Min(1.0, _ouchTicks / 30.0));
    }

    private void DrawHat(DrawingContext ctx, double bx, double by, bool flipH)
    {
        byte[,]? hat = _hat switch
        {
            HatType.Cowboy => HatCowboy, HatType.TopHat => HatTop,
            HatType.PartyHat => HatParty, HatType.Crown => HatCrown,
            HatType.Beret => HatBeret, _ => null
        };
        if (hat == null) return;

        var brush = _hat switch
        {
            HatType.Cowboy => HatBrown, HatType.TopHat => HatDark,
            HatType.PartyHat => HatPink, HatType.Crown => HatGold,
            HatType.Beret => HatRed, _ => HatBrown
        };

        var hatH = hat.GetLength(0);
        var hatW = hat.GetLength(1);
        var hx = bx + (GridW - hatW) / 2.0 * PixelSize;
        var hy = by - hatH * PixelSize + 1;
        DrawGrid(ctx, hat, hx, hy, flipH, brush);
    }

    private void DrawFriend(DrawingContext ctx)
    {
        const int fs = 2;
        var fh = FriendBody.GetLength(0) * fs;
        // Friend draws at an offset from the crab's local position
        var fx = CrabLocalX + _friendOffsetX;
        var fy = CrabLocalY + CrabH - fh;
        var flipF = _friendDir == -1;

        var rows = FriendBody.GetLength(0);
        var cols = FriendBody.GetLength(1);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var px = FriendBody[r, flipF ? (cols - 1 - c) : c];
                if (px == 0) continue;
                var brush = px == 2 ? Eye : Body;
                ctx.FillRectangle(brush, new Rect(fx + c * fs, fy + r * fs, fs, fs));
            }
    }

    private void DrawWeatherAccessory(DrawingContext ctx, double baseX, double baseY, bool flipH)
    {
        if (_weatherInfo == null) return;
        var ps = PixelSize;

        switch (_weatherInfo.Condition)
        {
            case Services.WeatherCondition.Rain:
            case Services.WeatherCondition.Thunderstorm:
                // Tiny umbrella held above — 5px wide handle sticking up from claw
                var ub = new SolidColorBrush(Color.Parse("#60a5fa"));
                var ux = flipH ? baseX - 2 * ps : baseX + CrabW - 2 * ps;
                var uy = baseY - 6 * ps;
                // Canopy
                for (var c = -2; c <= 2; c++)
                    ctx.FillRectangle(ub, new Rect(ux + c * ps, uy, ps, ps));
                for (var c = -1; c <= 1; c++)
                    ctx.FillRectangle(ub, new Rect(ux + c * ps, uy - ps, ps, ps));
                // Handle
                var handleB = new SolidColorBrush(Color.Parse("#94a3b8"));
                for (var r = 1; r <= 4; r++)
                    ctx.FillRectangle(handleB, new Rect(ux, uy + r * ps, ps, ps));
                // Animated raindrops falling around crab
                var dropB = new SolidColorBrush(Color.Parse("#3b82f680"));
                var off = (_tickCount / 4) % 6;
                ctx.FillRectangle(dropB, new Rect(baseX - 3 * ps, baseY + off * ps, ps, ps * 2));
                ctx.FillRectangle(dropB, new Rect(baseX + CrabW + 2 * ps, baseY + ((off + 3) % 6) * ps, ps, ps * 2));
                break;

            case Services.WeatherCondition.Clear:
                // Sunglasses on the eyes
                var sgB = new SolidColorBrush(Color.Parse("#1a1a1a"));
                var sgX = baseX + (flipH ? 3 : 5) * ps;
                var sgY = baseY + 5 * ps;
                // Left lens
                ctx.FillRectangle(sgB, new Rect(sgX, sgY, ps * 3, ps * 2));
                // Right lens
                ctx.FillRectangle(sgB, new Rect(sgX + 5 * ps, sgY, ps * 3, ps * 2));
                // Bridge
                ctx.FillRectangle(sgB, new Rect(sgX + 3 * ps, sgY, ps * 2, ps));
                // Shine on lenses
                var shineB = new SolidColorBrush(Color.Parse("#ffffff40"));
                ctx.FillRectangle(shineB, new Rect(sgX + ps, sgY, ps, ps));
                ctx.FillRectangle(shineB, new Rect(sgX + 6 * ps, sgY, ps, ps));
                break;

            case Services.WeatherCondition.Snow:
                // Scarf around neck area + snowflakes falling
                var scarfB = new SolidColorBrush(Color.Parse("#ef4444"));
                var scarfY = baseY + 12 * ps;
                for (var c = 2; c < 22; c++)
                    ctx.FillRectangle(scarfB, new Rect(baseX + c * ps, scarfY, ps, ps));
                // Dangling end
                var endX = flipH ? baseX + 20 * ps : baseX + 2 * ps;
                ctx.FillRectangle(scarfB, new Rect(endX, scarfY + ps, ps, ps * 2));
                ctx.FillRectangle(scarfB, new Rect(endX + ps, scarfY + ps, ps, ps * 2));
                // Snowflakes
                var snowB = new SolidColorBrush(Color.Parse("#e2e8f0"));
                var sOff = (_tickCount / 10) % 8;
                ctx.FillRectangle(snowB, new Rect(baseX - 4 * ps, baseY + sOff * ps, ps, ps));
                ctx.FillRectangle(snowB, new Rect(baseX + CrabW + 3 * ps, baseY + ((sOff + 4) % 8) * ps, ps, ps));
                ctx.FillRectangle(snowB, new Rect(baseX + 10 * ps, baseY - 2 * ps + ((sOff + 2) % 6) * ps, ps, ps));
                break;
        }
    }

    private void DrawParticles(DrawingContext ctx)
    {
        foreach (var p in _particles)
        {
            var alpha = Math.Clamp(1.0 - p.Age / p.MaxAge, 0, 1);
            switch (p.Type)
            {
                case ParticleType.Bubble:
                    var bBrush = new SolidColorBrush(Color.Parse("#88bbdd"), alpha * 0.5);
                    var bPen = new Pen(new SolidColorBrush(Color.Parse("#aaddff"), alpha * 0.6), 1);
                    ctx.DrawEllipse(bBrush, bPen, new Point(p.X, p.Y), p.Size, p.Size);
                    break;
                case ParticleType.Heart:
                    var hBrush = new SolidColorBrush(Color.Parse("#EF4444"), alpha);
                    foreach (var (dx, dy) in PxHeart)
                        ctx.FillRectangle(hBrush, new Rect(p.X + dx * p.Size, p.Y + dy * p.Size, p.Size, p.Size));
                    break;
                case ParticleType.Star:
                    var sBrush = new SolidColorBrush(Color.Parse("#F59E0B"), alpha);
                    foreach (var (dx, dy) in PxStar)
                        ctx.FillRectangle(sBrush, new Rect(p.X + dx * p.Size, p.Y + dy * p.Size, p.Size, p.Size));
                    break;
                case ParticleType.Confetti:
                    var colors = new[] { "#EF4444", "#F59E0B", "#4ade80", "#60a5fa", "#D946EF" };
                    var cBrush = new SolidColorBrush(Color.Parse(colors[(int)(p.X * 7) % colors.Length]), alpha);
                    ctx.FillRectangle(cBrush, new Rect(p.X, p.Y, 3, 3));
                    break;
            }
        }
    }

    private void DrawGrid(DrawingContext ctx, byte[,] grid, double ox, double oy, bool flipH, IBrush brush)
    {
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                if (grid[r, flipH ? (cols - 1 - c) : c] == 0) continue;
                ctx.FillRectangle(brush, new Rect(ox + c * PixelSize, oy + r * PixelSize, PixelSize, PixelSize));
            }
    }

    private void DrawPixels(DrawingContext ctx, (int c, int r)[] pixels, double ox, double oy, bool flipH, IBrush brush)
    {
        foreach (var (col, row) in pixels)
        {
            var c = flipH ? (GridW - 1 - col) : col;
            ctx.FillRectangle(brush, new Rect(ox + c * PixelSize, oy + row * PixelSize, PixelSize, PixelSize));
        }
    }

    private void DrawSleepZzz(DrawingContext ctx, double cx, double cy)
    {
        for (var i = 0; i < 3; i++)
        {
            var phase = (_zzzPhase + i * 1.2) % 3.6;
            if (phase > 3.0) continue;
            var floatUp = phase * 12;
            var alpha = phase < 0.5 ? phase * 2 : (phase > 2.5 ? (3.0 - phase) * 2 : 1.0);
            alpha = Math.Clamp(alpha, 0, 1);
            var zSize = Math.Max(1, (int)(PixelSize * (1 + i * 0.3) * 0.7));
            var zx = cx + 18 * PixelSize + i * 8;
            var zy = cy - floatUp - i * 6;
            var zBrush = new SolidColorBrush(Color.Parse("#8899aa"), alpha);
            foreach (var (dx, dy) in PxZ)
                ctx.FillRectangle(zBrush, new Rect(zx + dx * zSize, zy + dy * zSize, zSize, zSize));
        }
    }

    private void DrawYumBubble(DrawingContext ctx, double cx, double cy)
    {
        var alpha = Math.Clamp(_yumAlpha, 0, 1);
        var bx = cx + 4 * PixelSize;
        var by = cy + _yumY - 28;

        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#1e1e1e"), alpha * 0.9),
            new Rect(bx - 4, by - 4, 126, 20), 4);
        ctx.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#D77757"), alpha * 0.8), 1),
            new Rect(bx - 4, by - 4, 126, 20), 4);
        ctx.FillRectangle(new SolidColorBrush(Color.Parse("#1e1e1e"), alpha * 0.9),
            new Rect(bx + 10, by + 16, 8, 3));

        var ft = new FormattedText("yum yum spaghetti!",
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("monospace"), 10, new SolidColorBrush(Color.Parse("#f0c674"), alpha));
        ctx.DrawText(ft, new Point(bx, by - 1));
    }

    private void DrawFloatingText(DrawingContext ctx, string text, double x, double y, string color, double alpha)
    {
        var bounce = Math.Sin(_tickCount * 0.15) * 2;
        var ft = new FormattedText(text,
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("monospace", FontStyle.Normal, FontWeight.Bold), 9,
            new SolidColorBrush(Color.Parse(color), alpha));
        ctx.DrawText(ft, new Point(x, y + bounce));
    }

    #endregion
}
