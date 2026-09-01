using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Blink;

/// <summary>
/// A minimal, theme-friendly numeric up/down: a text box with spin buttons.
/// Supports a min/max range, a step increment, and a fixed number of decimals.
/// </summary>
public partial class NumericUpDown : System.Windows.Controls.UserControl
{
    // Guards against feedback loops between the Value property and the text box.
    private bool _updatingText;

    public NumericUpDown()
    {
        InitializeComponent();

        PART_Up.Click += (_, _) => Step(+1);
        PART_Down.Click += (_, _) => Step(-1);

        PART_TextBox.LostFocus += (_, _) => CommitText();
        PART_TextBox.PreviewTextInput += OnPreviewTextInput;
        PART_TextBox.PreviewKeyDown += OnPreviewKeyDown;

        Loaded += (_, _) => UpdateText();
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumericUpDown),
        new FrameworkPropertyMetadata(0.0,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(NumericUpDown),
        new PropertyMetadata(0.0, (d, _) => d.CoerceValue(ValueProperty)));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NumericUpDown),
        new PropertyMetadata(double.MaxValue, (d, _) => d.CoerceValue(ValueProperty)));

    public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register(
        nameof(Increment), typeof(double), typeof(NumericUpDown),
        new PropertyMetadata(1.0));

    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register(
        nameof(DecimalPlaces), typeof(int), typeof(NumericUpDown),
        new PropertyMetadata(0, (d, _) => ((NumericUpDown)d).UpdateText()));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var ctrl = (NumericUpDown)d;
        var v = (double)baseValue;
        return Math.Clamp(v, ctrl.Minimum, ctrl.Maximum);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NumericUpDown)d).UpdateText();

    private void Step(int direction)
    {
        Value = Value + direction * Increment;
        // Move the caret to the end after a spin so the value stays readable.
        PART_TextBox.CaretIndex = PART_TextBox.Text.Length;
    }

    private void UpdateText()
    {
        if (_updatingText || PART_TextBox is null)
            return;

        _updatingText = true;
        PART_TextBox.Text = Value.ToString("F" + Math.Max(0, DecimalPlaces), CultureInfo.CurrentCulture);
        _updatingText = false;
    }

    private void CommitText()
    {
        if (_updatingText)
            return;

        if (double.TryParse(PART_TextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
            Value = parsed; // coercion clamps to range
        else
            UpdateText(); // revert invalid input
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow digits, the culture decimal separator, and a leading minus.
        var sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        foreach (var ch in e.Text)
        {
            var s = ch.ToString();
            if (!char.IsDigit(ch) && s != sep && s != "-")
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                Step(+1);
                e.Handled = true;
                break;
            case Key.Down:
                Step(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                CommitText();
                e.Handled = true;
                break;
        }
    }
}
