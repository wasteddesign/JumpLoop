using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WDE.JumpLoop
{
    /// <summary>
    /// Interaction logic for JumpLoopCanvas.xaml
    /// </summary>
    public partial class JumpLoopCanvas : Canvas
    {
        public JumpLoopCanvas(JumpLoopMachine jlm, IPattern pattern)
        {   
            InitializeComponent();

            SolidColorBrush flashBrush = TryFindResource("BoxFlashBrush") as SolidColorBrush;
            this.Opacity = 0;
            this.Background = flashBrush;

            this.JumpLoopMachine = jlm;
            this.Pattern = pattern;

            JumpLoopMachine.PropertyChanged += JumpLoopMachine_PropertyChanged;

            Unloaded += (sender, e) =>
            {
                JumpLoopMachine.PropertyChanged -= JumpLoopMachine_PropertyChanged;
            };
        }

        private void JumpLoopMachine_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PatternEvent")
            {
                if (Pattern.PlayPosition >= 0 && !Pattern.IsPlayingSolo)
                {
                    SetAnimation(this, false);
                }
            }
        }

        private void SetAnimation(FrameworkElement fe, bool isVisible)
        {
            var myDoubleAnimation = new DoubleAnimation();
            if (isVisible)
            {
                myDoubleAnimation.From = 0.0;
                myDoubleAnimation.To = 1.0;
                fe.IsHitTestVisible = true;
            }
            else
            {
                myDoubleAnimation.From = 1.0;
                myDoubleAnimation.To = 0.0;
                fe.IsHitTestVisible = false;
            }
            myDoubleAnimation.Duration = new Duration(TimeSpan.FromSeconds(1.0));
            myDoubleAnimation.AutoReverse = false;

            Storyboard myStoryboard = new Storyboard();
            myStoryboard.Children.Add(myDoubleAnimation);
            Storyboard.SetTargetProperty(myDoubleAnimation, new PropertyPath(Canvas.OpacityProperty));
            myStoryboard.Begin(fe);
        }

        public JumpLoopMachine JumpLoopMachine { get; private set; }
        public IPattern Pattern { get; private set; }
    }
}
