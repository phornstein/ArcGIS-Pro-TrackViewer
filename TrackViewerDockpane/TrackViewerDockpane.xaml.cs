using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Table = ArcGIS.Core.Data.Table;

namespace ProTrackViewer
{
    /// <summary>
    /// Interaction logic for TrackViewerDockpaneView.xaml
    /// </summary>
    public partial class TrackViewerDockpaneView : UserControl
    {
        public TrackViewerDockpaneView()
        {
            InitializeComponent();
        }

        private void ComboBox_TrackFCSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ComboBox_TrackFieldSelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void CreateLayerFromSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedLayer = FeatureClassCombobox.SelectedItem;
            MapView mapView = MapView.Active;
            FeatureLayer fc = (FeatureLayer) FeatureClassCombobox.SelectedItem;
            string field = FieldsCombobox.SelectedItem.ToString();
            string trk = TracksCombobox.SelectedItem.ToString();
            string query = field + " = '" + trk + "'";

            QueuedTask.Run(() =>
            {
                if (LayerFactory.Instance.CanCopyLayer(fc)) 
                { 
                    FeatureLayer selectionLayer = LayerFactory.Instance.CopyLayer(fc, MapView.Active.Map) as FeatureLayer;
                    QueuedTask.Run(() =>
                    {
                        selectionLayer.SetDefinitionQuery(query);
                    });
                } 
            });
            FeatureClassCombobox.SelectedItem = selectedLayer;

        }

        private void CopyAttributes_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var i in this.TrackAttrsListBox.SelectedItems)
            {
                string value = i.ToString().Split(':')[1].Trim();
                sb.Append(value);
                sb.Append("\n");
            }
            Clipboard.SetText(sb.ToString());
        }
    }
}
