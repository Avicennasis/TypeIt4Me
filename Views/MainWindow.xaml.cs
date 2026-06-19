using System;
using System.Windows;
using TypeIt4Me.ViewModels;

namespace TypeIt4Me.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
               vm.RequestWindowResize += (mini) =>
               {
                   if (mini)
                   {
                       this.Width = 200;
                       this.Height = 150;
                       // Keep resizable
                       this.ResizeMode = ResizeMode.CanResize;
                   }
                   else
                   {
                       this.Width = 350;
                       this.Height = 500;
                       this.ResizeMode = ResizeMode.CanResize;
                   }
               };
               
               if (vm.IsMiniMode)
               {
                   this.Width = 200;
                   this.Height = 150;
                   this.ResizeMode = ResizeMode.CanResize;
               }
            }
        }



        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Application.Current.Shutdown();
            }
            catch { }
            finally
            {
                Environment.Exit(0);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                if (DataContext is MainViewModel vm && vm.MinimizeToTray)
                {
                    this.Hide();
                }
            }
            base.OnStateChanged(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                
                if (vm.MinimizeToTray)
                {
                    e.Cancel = true;
                    this.Hide();
                }
                else
                {
                    // Ensure full shutdown for debugging purposes if implicit shutdown fails
                    try
                    {
                        Application.Current.Shutdown();
                    }
                    catch { }
                    finally
                    {
                        Environment.Exit(0);
                    }
                }
            }
        }
    }
}
