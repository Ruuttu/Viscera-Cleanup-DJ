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
    /// Interaction logic for ConversionDialog.xaml
    /// </summary>
    public partial class ConversionDialog : Window
    {
        public ConversionDialog()
        {
            InitializeComponent();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
