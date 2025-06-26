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
        private Storyboard? _arrowBounceAnimation;
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
            _arrowBounceAnimation = (Storyboard)FindResource("ArrowBounceAnimation");
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

            // Position the card intelligently based on target element
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

            // If this step has a target element, position card intelligently relative to it
            if (step.Type == TourStepType.Highlight && !string.IsNullOrEmpty(step.TargetElement))
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var targetElement = mainWindow.FindName(step.TargetElement) as FrameworkElement;
                    if (targetElement != null)
                    {
                        PositionCardRelativeToTarget(targetElement);
                        return;
                    }
                }
            }

            // Fallback to manual positioning
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

        private void PositionCardRelativeToTarget(FrameworkElement targetElement)
        {
            try
            {
                // Get target element position relative to screen
                var elementPosition = targetElement.PointToScreen(new Point(0, 0));
                var elementSize = new Size(targetElement.ActualWidth, targetElement.ActualHeight);

                // Get screen dimensions
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                // Calculate element center
                var elementCenterX = elementPosition.X + elementSize.Width / 2;
                var elementCenterY = elementPosition.Y + elementSize.Height / 2;

                // Estimated tour card size (will be adjusted by WPF)
                var cardWidth = 400.0;
                var cardHeight = 300.0;

                // Determine best position for tour card
                var cardX = 0.0;
                var cardY = 0.0;

                // Try to position card in the most visible area
                if (elementCenterX < screenWidth / 2)
                {
                    // Element is on left side, place card on right
                    cardX = elementPosition.X + elementSize.Width + 50;
                    if (cardX + cardWidth > screenWidth - 50)
                    {
                        // Not enough space on right, place below
                        cardX = Math.Max(50, elementCenterX - cardWidth / 2);
                        cardY = elementPosition.Y + elementSize.Height + 50;
                    }
                    else
                    {
                        cardY = Math.Max(50, elementCenterY - cardHeight / 2);
                    }
                }
                else
                {
                    // Element is on right side, place card on left
                    cardX = elementPosition.X - cardWidth - 50;
                    if (cardX < 50)
                    {
                        // Not enough space on left, place below
                        cardX = Math.Max(50, elementCenterX - cardWidth / 2);
                        cardY = elementPosition.Y + elementSize.Height + 50;
                    }
                    else
                    {
                        cardY = Math.Max(50, elementCenterY - cardHeight / 2);
                    }
                }

                // Ensure card doesn't go off screen
                cardX = Math.Max(20, Math.Min(cardX, screenWidth - cardWidth - 20));
                cardY = Math.Max(20, Math.Min(cardY, screenHeight - cardHeight - 20));

                // Position the tour card
                TourCard.HorizontalAlignment = HorizontalAlignment.Left;
                TourCard.VerticalAlignment = VerticalAlignment.Top;
                TourCard.Margin = new Thickness(cardX, cardY, 0, 0);

                Console.WriteLine($"[TOUR] Kart konumlandırıldı: Element({elementPosition.X}, {elementPosition.Y}) -> Kart({cardX}, {cardY})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Tur kartı konumlandırılırken hata: {ex.Message}");
                // Fallback to center
                TourCard.HorizontalAlignment = HorizontalAlignment.Center;
                TourCard.VerticalAlignment = VerticalAlignment.Center;
                TourCard.Margin = new Thickness(20);
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

                // Set arrow rotation for all arrow elements
                ArrowRotate.Angle = arrowRotation;
                ArrowShadowRotate.Angle = arrowRotation;
                ArrowHighlightRotate.Angle = arrowRotation;

                ArrowCanvas.Visibility = Visibility.Visible;

                // Start animations
                if (_arrowPulseAnimation != null)
                {
                    _arrowPulseAnimation.Begin(ArrowPath);
                }

                if (_arrowBounceAnimation != null)
                {
                    _arrowBounceAnimation.Begin(ArrowPath);
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

            // Arrow distance from element (increased for better visibility)
            var arrowDistance = 80;
            var arrowX = 0.0;
            var arrowY = 0.0;

            // Determine best arrow position based on element location and available space
            if (elementCenterX < screenWidth / 3)
            {
                // Element is on left side - place arrow on left, pointing right
                arrowX = elementPosition.X - arrowDistance;
                arrowY = elementCenterY - 25; // Center arrow vertically with element

                if (arrowX < 20)
                {
                    // Not enough space on left, place below
                    arrowX = elementCenterX - 30;
                    arrowY = elementPosition.Y + elementSize.Height + 30;
                }
            }
            else if (elementCenterX > screenWidth * 2 / 3)
            {
                // Element is on right side - place arrow on right, pointing left
                arrowX = elementPosition.X + elementSize.Width + 30;
                arrowY = elementCenterY - 25;

                if (arrowX + 80 > screenWidth - 20)
                {
                    // Not enough space on right, place below
                    arrowX = elementCenterX - 30;
                    arrowY = elementPosition.Y + elementSize.Height + 30;
                }
            }
            else
            {
                // Element is in center - place arrow above or below based on vertical position
                if (elementCenterY < screenHeight / 2)
                {
                    // Element is in upper half, place arrow above
                    arrowX = elementCenterX - 30;
                    arrowY = elementPosition.Y - 60;
                }
                else
                {
                    // Element is in lower half, place arrow below
                    arrowX = elementCenterX - 30;
                    arrowY = elementPosition.Y + elementSize.Height + 30;
                }
            }

            // Ensure arrow stays within screen bounds
            arrowX = Math.Max(20, Math.Min(arrowX, screenWidth - 100));
            arrowY = Math.Max(20, Math.Min(arrowY, screenHeight - 80));

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

            // Stop all animations
            if (_arrowPulseAnimation != null)
            {
                _arrowPulseAnimation.Stop(ArrowPath);
            }

            if (_arrowBounceAnimation != null)
            {
                _arrowBounceAnimation.Stop(ArrowPath);
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
