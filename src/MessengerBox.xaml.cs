using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Viscera_Cleanup_DJ
{
    /// <summary>
    /// Interaction logic for MessageBox.xaml
    /// </summary>
    public partial class MessengerBox : Window
    {
        public Button ClickedButton;
        public Button FocusButton;
        public bool Escaped;

        public MessengerBox(Window owner = null)
        {
            InitializeComponent();

            if (owner != null)
            {
                Owner = owner;
            }

            PreviewKeyDown += new KeyEventHandler(HandleKeyEvent);
        }

        private void HandleKeyEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Escaped = true;
                Close();
            }
        }

        public new void ShowDialog()
        {
            if (ButtonStack.Children.Count == 0)
            {
                AddButton("Okay", true);
            }
            Escaped = true;
            base.ShowDialog();
        }

        public Button AddButton(String content, bool defaultButton = false)
        {
            Button button = new Button();
            button.Content = content;
            button.Padding = new Thickness(12, 0, 12, 0);
            button.Margin = new Thickness(10, 10, 10, 10);
            button.MinWidth = 60;
            button.IsTabStop = true;
            button.Click += ButtonClickEvent;

            ButtonStack.Children.Insert(0, button);
            if (ButtonStack.Children.Count > 1)
            {
                var lastButton = (Button) ButtonStack.Children[1];
                lastButton.Margin = new Thickness(0, 10, 10, 10);
            }

            if (defaultButton)
            {
                button.IsDefault = true;
            }

            return button;
        }

        public void AddElement(UIElement element)
        {
            MainGrid.Children.Add(element);
        }

        public static void Information(Window owner, string text)
        {
            MessengerBox dialog = new MessengerBox(owner);
            dialog.Message.Text = text;
            dialog.ShowDialog();
        }

        public static void Error(Window owner, string text)
        {
            MessengerBox dialog = new MessengerBox(owner);
            dialog.Message.Text = text;
            dialog.Title = "Error";

            System.Media.SystemSounds.Beep.Play();
            dialog.ShowDialog();
        }

        public void ButtonClickEvent(object sender, RoutedEventArgs e)
        {
            ClickedButton = (Button) sender;
            Escaped = false;
            Close();
        }
    }
}
