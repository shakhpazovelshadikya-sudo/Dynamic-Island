using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DynamicIsland;

/// <summary>
/// Питомец в левой части капсулы. Настроение меняется само:
/// сидит (моргает, виляет хвостом), гуляет туда-сюда, спит (над ним плывут «z»), пританцовывает, пока играет музыка,
/// и подпрыгивает от радости: при смене трека, когда остров свернулся обратно или после уведомления.
/// </summary>
public partial class MainWindow
{
    private enum PetMood { Sit, Walk, Sleep, Jump, Dance }

    /// <summary>Сколько места питомец отнимает у текста в капсуле.</summary>
    private const double PetExtra = 36;
    private const double PetWalkRange = 8;

    private static readonly string[] SitFrames = { "sit0", "sit0", "sit0", "sit2", "sit0", "sit0", "sit2", "sit1" };
    private static readonly Random Rng = new();

    private readonly DispatcherTimer _petTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private PetDef? _pet;
    private PetMood _petMood = PetMood.Sit;
    private string? _petFrame;
    private int _petTick, _petTicksLeft, _petJumpLength = 5;
    private double _petX, _petDir = 1;
    private DateTime _petLastActivity = DateTime.Now;

    /// <summary>Левый отступ текста в капсуле: с питомцем он больше.</summary>
    private double CompactLeftMargin => _pet != null ? 42 + PetExtra : 42;

    private void InitPet()
    {
        _petTimer.Tick += (_, _) => PetTick();
        ApplyPet();
    }

    /// <summary>Применяет выбор питомца из настроек.</summary>
    private void ApplyPet()
    {
        var def = PetSprites.Find(_settings.Pet);
        bool changed = def?.Id != _pet?.Id;
        _pet = def;

        PetImage.Visibility = def != null ? Visibility.Visible : Visibility.Collapsed;
        CompactLangBadge.Margin = new Thickness(def != null ? 9 + PetExtra : 9, 0, 0, 0);
        CompactTextHost.Margin = new Thickness(CompactLeftMargin, 0, CompactTextHost.Margin.Right, 0);

        if (def == null)
        {
            _petTimer.Stop();
            StopZ();
            return;
        }

        if (changed)
        {
            _petFrame = null;
            _petX = 0;
            PetShift.X = 0;
            SetMood(PetMood.Sit);
        }
        if (!_petTimer.IsEnabled) _petTimer.Start();
    }

    /// <summary>Питомец радуется: подпрыгивает (если не танцует). Заодно считается «активностью» — будит спящего.</summary>
    private void PetReact()
    {
        _petLastActivity = DateTime.Now;
        if (_pet != null && _petMood != PetMood.Dance) SetMood(PetMood.Jump);
    }

    private void SetMood(PetMood mood)
    {
        _petMood = mood;
        _petTick = 0;
        PetShift.Y = 0;

        _petTicksLeft = mood switch
        {
            PetMood.Sit => Rng.Next(40, 110),    // 5–13 с
            PetMood.Walk => Rng.Next(20, 45),    // 2,5–5 с
            PetMood.Sleep => Rng.Next(150, 400), // 18–48 с
            PetMood.Jump => _petJumpLength,
            _ => int.MaxValue                    // танец идёт, пока играет музыка
        };

        if (mood == PetMood.Sleep) StartZ(); else StopZ();
        ShowPetFrame(mood switch
        {
            PetMood.Walk => "walk0",
            PetMood.Sleep => "sleep0",
            PetMood.Jump => "jump",
            PetMood.Dance => "dance0",
            _ => "sit0"
        });
    }

    private void PetTick()
    {
        if (_pet == null) return;
        if (_expanded || _notifActive || !IsVisible) return; // питомца не видно — не тратим силы

        _petTick++;

        bool music = _info.HasSession && _info.IsPlaying;
        if (music && _petMood != PetMood.Dance) { SetMood(PetMood.Dance); return; }
        if (!music && _petMood == PetMood.Dance) { SetMood(PetMood.Sit); return; }

        switch (_petMood)
        {
            case PetMood.Sit:
                ShowPetFrame(SitFrames[_petTick / 4 % SitFrames.Length]);
                break;

            case PetMood.Walk:
                ShowPetFrame(_petTick / 2 % 2 == 0 ? "walk0" : "walk1");
                _petX += _petDir * 0.75;
                if (_petX >= PetWalkRange) { _petX = PetWalkRange; _petDir = -1; }
                else if (_petX <= 0) { _petX = 0; _petDir = 1; }
                PetShift.X = _petX;
                PetFlip.ScaleX = _petDir;
                break;

            case PetMood.Sleep:
                ShowPetFrame(_petTick / 6 % 2 == 0 ? "sleep0" : "sleep1");
                break;

            case PetMood.Dance:
                ShowPetFrame(_petTick / 2 % 2 == 0 ? "dance0" : "dance1");
                break;

            case PetMood.Jump:
                // дуга прыжка: вверх на 6 px и обратно
                PetShift.Y = -6 * Math.Sin(Math.PI * _petTick / _petJumpLength);
                break;
        }

        if (--_petTicksLeft <= 0) NextMood();
    }

    private void NextMood()
    {
        bool music = _info.HasSession && _info.IsPlaying;
        if (music) { SetMood(PetMood.Dance); return; }

        double idle = (DateTime.Now - _petLastActivity).TotalSeconds;
        int roll = Rng.Next(100);

        var next = idle > 45
            ? (roll < 65 ? PetMood.Sleep : roll < 85 ? PetMood.Sit : PetMood.Walk)  // давно никого нет — клонит в сон
            : (roll < 50 ? PetMood.Sit : roll < 85 ? PetMood.Walk : idle > 20 ? PetMood.Sleep : PetMood.Sit);

        if (next == PetMood.Walk && _petMood == PetMood.Sleep) next = PetMood.Sit; // сначала проснуться
        SetMood(next);
    }

    private void ShowPetFrame(string key)
    {
        if (_pet == null) return;
        var drawn = PetSprites.Resolve(_pet, DateTime.Now); // «Солнце и луна» меняется по времени суток
        string cacheKey = drawn.Id + "/" + key;
        if (cacheKey == _petFrame) return;
        _petFrame = cacheKey;
        PetImage.Source = PetSprites.GetFrame(drawn, key);
    }

    // ---------- «z-z-z» над спящим ----------
    private void StartZ()
    {
        PetZShift.X = _petX;
        PetZ.Visibility = Visibility.Visible;
        PetZShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(5, -3, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });
        PetZ.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.1, 1, TimeSpan.FromSeconds(0.9)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }

    private void StopZ()
    {
        PetZShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        PetZ.BeginAnimation(OpacityProperty, null);
        PetZ.Visibility = Visibility.Collapsed;
    }
}
