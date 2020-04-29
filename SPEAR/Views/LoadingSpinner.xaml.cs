using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
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
using System.Windows.Threading;

namespace SPEAR.Views
{
    /// <summary>
    /// Interaction logic for LoadingSpinner.xaml
    /// </summary>
    public partial class LoadingSpinner : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        /////////////////////////////////////////////////////////////////////////
        // Properties
        /////////////////////////////////////////////////////////////////////////
        private string displayedText;
        public string DisplayedText
        {
            get { return displayedText; }
            private set { SetProperty(ref displayedText, value, nameof(DisplayedText)); }
        }

        private Visibility spinnerVisibility;
        public Visibility SpinnerVisibility
        {
            get { return spinnerVisibility; }
            private set { SetProperty(ref spinnerVisibility, value, nameof(SpinnerVisibility)); }
        }

        private bool isSpinnerEnabled;
        public bool IsSpinnerEnabled
        {
            get { return isSpinnerEnabled; }
            private set { SetProperty(ref isSpinnerEnabled, value, nameof(IsSpinnerEnabled)); }
        }

        // Note: The StoryBoard transforms the shaded ellipse.
        private Storyboard spinnerStoryBoard { get; set; }

        // Note: The timerWorker starts and stops the timer to make the timer event
        // fire on the worker instead of the main thread. If the timer is started on
        // the main thread, the main thread would be the one handling the updates.
        //private static BackgroundWorker timerWorker { get; set; }

        //private DispatcherTimer percentTimer { get; set; }
        private double timeElapsed { get; set; }
        private double timeNeeded { get; set; }


        /////////////////////////////////////////////////////////////////////////
        // Constructor
        /////////////////////////////////////////////////////////////////////////
        public LoadingSpinner()
        {
            InitializeComponent();

            DisplayedText = "";
            SpinnerVisibility = Visibility.Collapsed;
            IsSpinnerEnabled = false;
            spinnerStoryBoard = this.Resources["StoryboardKey"] as Storyboard;
            spinnerStoryBoard.Begin(this, true);
            _spinnerUserControl.DataContext = this;
            //dotdotCounter = 0;

            //timerWorker = new BackgroundWorker();
            //timerWorker.DoWork += TimerWorker_DoWork;
            //timerWorker.RunWorkerAsync();
        }


        /////////////////////////////////////////////////////////////////////////
        // Methods
        /////////////////////////////////////////////////////////////////////////
        public void StartText(string spinnerText = "")
        {
            DisplayedText = spinnerText;

            if (IsSpinnerEnabled)
                return;
            else
            {
                SpinnerVisibility = Visibility.Visible;
                IsSpinnerEnabled = true;
                spinnerStoryBoard.Resume();
                //if (percentTimer.IsEnabled == false)
                //    percentTimer.Start();
            }
        }

        public void StopText()
        {
            if (IsSpinnerEnabled)
            {
                SpinnerVisibility = Visibility.Collapsed;
                IsSpinnerEnabled = false;
                spinnerStoryBoard.Pause();
                //if (percentTimer.IsEnabled)
                //    percentTimer.Stop();
            }
        }

        //private void Timer_Tick(object sender, EventArgs e)
        //{
        //    switch (dotdotCounter++)
        //    {
        //        case 0:
        //            DisplayedText = string.Concat(originalText, string.Empty);
        //            break;
        //        case 1:
        //            DisplayedText = string.Concat(originalText, ".");
        //            break;
        //        case 2:
        //            DisplayedText = string.Concat(originalText, "..");
        //            dotdotCounter = 0;
        //            break;
        //        default:
        //            dotdotCounter = 0;
        //            break;
        //    }
        //}

        //private void TimerWorker_DoWork(object sender, DoWorkEventArgs e)
        //{
        //    percentTimer = new DispatcherTimer(
        //        new TimeSpan(0, 0, 0, 0, 700),
        //        DispatcherPriority.Background,
        //        Timer_Tick,
        //        Application.Current.Dispatcher
        //        );
        //    percentTimer.Stop();
        //}
    }
}
