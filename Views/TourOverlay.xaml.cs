using System.Windows;
using System.Windows.Media.Animation;
using OtomatikMetinGenisletici.Models;
using OtomatikMetinGenisletici.Services;

namespace OtomatikMetinGenisletici.Views
{
    public partial class TourOverlay : Window
    {
        private readonly ITourService _tourService;
        private Storyboard? _arrowPulseAnimation;
        private Storyboard? _sparkleAnimation;
        private Storyboard? _fadeInAnimation;

        public TourOverlay(ITourService tourService)
        {
            InitializeComponent();
            _tourService = tourService;
            
            // Event handlers
            _tourService.StepChanged += OnStepChanged;
            _tourService.TourCompleted += OnTourCompleted;
            _tourService.TourSkipped += OnTourSkipped;
            
            // Animations
            _arrowPulseAnimation = (Storyboard)FindResource("ArrowPulseAnimation");
            _sparkleAnimation = (Storyboard)FindResource("SparkleAnimation");
            _fadeInAnimation = (Storyboard)FindResource("FadeInAnimation");
            
            // Initial setup
            Loaded += TourOverlay_Loaded;
        }

        private void TourOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            // Start fade in animation
            if (_fadeInAnimation != null)
            {
                _fadeInAnimation.Begin(TourCard);
            }
        }

        private void OnStepChanged(TourStep step)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTourCard(step);
                UpdateHighlight(step);
                UpdateProgress();
            });
        }

        private void UpdateTourCard(TourStep step)
        {
            IconText.Text = step.Icon;
            TitleText.Text = step.Title;
            DescriptionText.Text = step.Description;
            StepCounter.Text = $"{_tourService.CurrentStepIndex + 1} / {_tourService.TotalSteps}";
            
            // Button states
            PreviousButton.IsEnabled = _tourService.CurrentStepIndex > 0;
            SkipButton.Visibility = step.IsSkippable ? Visibility.Visible : Visibility.Collapsed;
            
            // Next button text
            if (_tourService.CurrentStepIndex == _tourService.TotalSteps - 1)
            {
                NextButton.Content = "Tamamla 🎉";
            }
            else
            {
                NextButton.Content = !string.IsNullOrEmpty(step.ActionText) ? step.ActionText + " ▶" : "Devam ▶";
            }

            // Position the card
            PositionTourCard(step);
            
            // Restart fade in animation
            if (_fadeInAnimation != null)
            {
                _fadeInAnimation.Begin(TourCard);
            }
        }

        private void PositionTourCard(TourStep step)
        {
            // Reset alignment
            TourCard.HorizontalAlignment = HorizontalAlignment.Center;
            TourCard.VerticalAlignment = VerticalAlignment.Center;
            TourCard.Margin = new Thickness(20);

            switch (step.Position)
            {
                case TourStepPosition.Top:
                    TourCard.VerticalAlignment = VerticalAlignment.Top;
                    TourCard.Margin = new Thickness(20, 50, 20, 20);
                    break;
                case TourStepPosition.Bottom:
                    TourCard.VerticalAlignment = VerticalAlignment.Bottom;
                    TourCard.Margin = new Thickness(20, 20, 20, 50);
                    break;
                case TourStepPosition.Left:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Left;
                    TourCard.Margin = new Thickness(50, 20, 20, 20);
                    break;
                case TourStepPosition.Right:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Right;
                    TourCard.Margin = new Thickness(20, 20, 50, 20);
                    break;
                case TourStepPosition.TopLeft:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Left;
                    TourCard.VerticalAlignment = VerticalAlignment.Top;
                    TourCard.Margin = new Thickness(50, 50, 20, 20);
                    break;
                case TourStepPosition.TopRight:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Right;
                    TourCard.VerticalAlignment = VerticalAlignment.Top;
                    TourCard.Margin = new Thickness(20, 50, 50, 20);
                    break;
                case TourStepPosition.BottomLeft:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Left;
                    TourCard.VerticalAlignment = VerticalAlignment.Bottom;
                    TourCard.Margin = new Thickness(50, 20, 20, 50);
                    break;
                case TourStepPosition.BottomRight:
                    TourCard.HorizontalAlignment = HorizontalAlignment.Right;
                    TourCard.VerticalAlignment = VerticalAlignment.Bottom;
                    TourCard.Margin = new Thickness(20, 20, 50, 50);
                    break;
                case TourStepPosition.Custom:
                    if (step.CustomPosition.HasValue)
                    {
                        TourCard.HorizontalAlignment = HorizontalAlignment.Left;
                        TourCard.VerticalAlignment = VerticalAlignment.Top;
                        TourCard.Margin = new Thickness(step.CustomPosition.Value.X, step.CustomPosition.Value.Y, 0, 0);
                    }
                    break;
            }
        }

        private void UpdateHighlight(TourStep step)
        {
            if (step.Type == TourStepType.Highlight && !string.IsNullOrEmpty(step.TargetElement))
            {
                // Find target element in main window
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var targetElement = mainWindow.FindName(step.TargetElement) as FrameworkElement;
                    if (targetElement != null)
                    {
                        ShowArrowPointer(targetElement);
                        return;
                    }
                }
            }

            HideArrowPointer();
        }

        private void ShowArrowPointer(FrameworkElement targetElement)
        {
            try
            {
                // Get element position relative to screen
                var elementPosition = targetElement.PointToScreen(new Point(0, 0));
                var elementSize = new Size(targetElement.ActualWidth, targetElement.ActualHeight);

                // Calculate arrow position and direction
                var arrowPosition = CalculateArrowPosition(elementPosition, elementSize);
                var arrowRotation = CalculateArrowRotation(arrowPosition, elementPosition, elementSize);

                // Position arrow canvas
                ArrowCanvas.Margin = new Thickness(arrowPosition.X, arrowPosition.Y, 0, 0);
                ArrowCanvas.HorizontalAlignment = HorizontalAlignment.Left;
                ArrowCanvas.VerticalAlignment = VerticalAlignment.Top;

                // Set arrow rotation
                ArrowRotate.Angle = arrowRotation;
                ArrowShadowRotate.Angle = arrowRotation;

                ArrowCanvas.Visibility = Visibility.Visible;

                // Start animations
                if (_arrowPulseAnimation != null)
                {
                    _arrowPulseAnimation.Begin(ArrowPath);
                }

                if (_sparkleAnimation != null)
                {
                    _sparkleAnimation.Begin(ArrowCanvas);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Arrow pointer gösterilirken hata: {ex.Message}");
                HideArrowPointer();
            }
        }

        private Point CalculateArrowPosition(Point elementPosition, Size elementSize)
        {
            // Calculate the best position for arrow based on element location
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            var elementCenterX = elementPosition.X + elementSize.Width / 2;
            var elementCenterY = elementPosition.Y + elementSize.Height / 2;

            // Default: point from top-left
            var arrowX = elementPosition.X - 60;
            var arrowY = elementPosition.Y - 40;

            // Adjust if too close to screen edges
            if (arrowX < 20)
            {
                // Point from right
                arrowX = elementPosition.X + elementSize.Width + 20;
                arrowY = elementPosition.Y + elementSize.Height / 2 - 15;
            }
            else if (arrowY < 20)
            {
                // Point from bottom
                arrowX = elementPosition.X + elementSize.Width / 2 - 15;
                arrowY = elementPosition.Y + elementSize.Height + 20;
            }
            else if (elementCenterX > screenWidth * 0.7)
            {
                // Point from left
                arrowX = elementPosition.X - 60;
                arrowY = elementPosition.Y + elementSize.Height / 2 - 15;
            }

            return new Point(arrowX, arrowY);
        }

        private double CalculateArrowRotation(Point arrowPosition, Point elementPosition, Size elementSize)
        {
            var elementCenterX = elementPosition.X + elementSize.Width / 2;
            var elementCenterY = elementPosition.Y + elementSize.Height / 2;

            var deltaX = elementCenterX - arrowPosition.X;
            var deltaY = elementCenterY - arrowPosition.Y;

            // Calculate angle in degrees
            var angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;

            // Adjust for arrow shape (pointing right by default)
            return angle;
        }

        private void HideArrowPointer()
        {
            ArrowCanvas.Visibility = Visibility.Collapsed;

            if (_arrowPulseAnimation != null)
            {
                _arrowPulseAnimation.Stop(ArrowPath);
            }

            if (_sparkleAnimation != null)
            {
                _sparkleAnimation.Stop(ArrowCanvas);
            }
        }

        private void UpdateProgress()
        {
            var progress = (double)(_tourService.CurrentStepIndex + 1) / _tourService.TotalSteps * 100;
            ProgressBar.Value = progress;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _ = _tourService.NextStepAsync();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            _ = _tourService.PreviousStepAsync();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            _ = _tourService.SkipTourAsync();
        }

        private void OnTourCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                HideArrowPointer();
                Close();
            });
        }

        private void OnTourSkipped()
        {
            Dispatcher.Invoke(() =>
            {
                HideArrowPointer();
                Close();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup event handlers
            _tourService.StepChanged -= OnStepChanged;
            _tourService.TourCompleted -= OnTourCompleted;
            _tourService.TourSkipped -= OnTourSkipped;
            
            base.OnClosed(e);
        }
    }
}
