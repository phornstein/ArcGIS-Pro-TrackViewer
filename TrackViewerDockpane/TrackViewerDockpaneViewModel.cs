using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace ProTrackViewer
{
    internal class TrackViewerDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "ProTrackViewer_TrackViewerDockpane";

        protected TrackViewerDockpaneViewModel() {
            ActiveMapViewChangedEvent.Subscribe(OnMapViewChanged);
            LayersAddedEvent.Subscribe(OnLayersChanged);
            LayersRemovedEvent.Subscribe(OnLayersChanged);
        }
        ~TrackViewerDockpaneViewModel()
        {
            ActiveMapViewChangedEvent.Unsubscribe(OnMapViewChanged);
            LayersAddedEvent.Unsubscribe(OnLayersChanged);
            LayersRemovedEvent.Unsubscribe(OnLayersChanged);
            ClearFields();
        }

            /// <summary>
            /// Show the DockPane.
            /// </summary>
            internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        public void OnLayersChanged(LayerEventsArgs obj)
        {
            var selected = this.SelectedFeatureLayer; //store current selection
            this.NotifyPropertyChanged("FeatureClasses"); //wipes current selection when rebuilding list
            this.SelectedFeatureLayer = selected; //reset current selection
            this.NotifyPropertyChanged("SelectedFeatureLayer"); //let combobox know select has been updated
        }

        public void OnMapViewChanged(ActiveMapViewChangedEventArgs obj)
        {
            ClearFields();
            this.NotifyPropertyChanged("FeatureClasses");
            this.NotifyPropertyChanged("SelectedField");
        }

        private void ClearFields()
        {
            this.FeatureClasses = new ObservableCollection<Layer>();
            this.FeatureLayerFields = new ObservableCollection<string>();
            this.SelectedFeatureLayer = null;
            this.TrackUIDs = new ObservableCollection<string>();
            this.SelectedField = null;
            this.SelectedTrack = null;
            this.TrackAttributes = new ObservableCollection<string>();
            this.SelectedSortMethod = null;
        }

        private ObservableCollection<Layer> _featureclasses = new ObservableCollection<Layer>();
        public ObservableCollection<Layer> FeatureClasses
        {
            /*Managed the list of featurelayers in the combobox*/
            get {
                if (MapView.Active != null)
                {
                    _featureclasses.Clear();
                    var lyrs = MapView.Active.Map.GetLayersAsFlattenedList().Where(lyr => lyr.GetType().ToString().Equals("ArcGIS.Desktop.Mapping.FeatureLayer"));
                    foreach (var l in lyrs)
                    {
                        _featureclasses.Add(l); ;
                    }
                }
                return _featureclasses;
            }
            set { this._featureclasses = value; }
        }

        private ObservableCollection<string> _fields = new ObservableCollection<string>();
        public ObservableCollection<string> FeatureLayerFields
        {
            /*Manages the list of fields from the selected featurelayer*/
            get
            { return _fields; }
            set { 
                _fields = value;
            }
        }

        private FeatureLayer _selectedFeatureLayer = null; //the selected featurelayer
        public FeatureLayer SelectedFeatureLayer
        {
            get { return _selectedFeatureLayer; } //return selected feature
            set { 
                _selectedFeatureLayer = value;

                if (value != null)
                {
                    QueuedTask.Run(() =>
                    {
                        FeatureLayer lyr = _selectedFeatureLayer;
                        Table tbl = lyr.GetTable();
                        ObservableCollection<string> fs = new ObservableCollection<string>();
                        if (tbl != null)
                        {
                            TableDefinition def = tbl.GetDefinition();
                            IReadOnlyList<Field> fields = def.GetFields();
                            foreach (var f in fields)
                            {
                                fs.Add(f.Name);
                            }
                        }
                        this.FeatureLayerFields = fs;
                        this.NotifyPropertyChanged("FeatureLayerFields");
                    });
                }
            } //set selected feature (the combobox uses this), value is what is passed to it
        }

        private ObservableCollection<string> _uids = new ObservableCollection<string>();
        public ObservableCollection<string> TrackUIDs
        {
            get { return _uids; }
            set { _uids = value; }
        }

        private Dictionary<string, TrackViewerDockpane.Track> _trackAttrs = new Dictionary<string, TrackViewerDockpane.Track>();
        private string _selectedField = null;
        private string _timeField = null;
        public string SelectedField
        {
            get { return _selectedField; }
            set
            {
                _selectedField = value;

                if (value != null)
                {
                    int ind;

                    var pd = new ArcGIS.Desktop.Framework.Threading.Tasks.ProgressDialog("Loading tracks...");
                    pd.Show();
                    QueuedTask.Run(() =>
                    {
                        ObservableCollection<string> uids = new ObservableCollection<string>();
                        FeatureLayer lyr = _selectedFeatureLayer;
                        FeatureClass fc = lyr.GetFeatureClass();
                        QueryFilter query = new QueryFilter() { ObjectIDs = lyr.GetSelection().GetObjectIDs() };
                        Map map = MapView.Active.Map;
                        List<string> failedTracks = new List<string>();

                        var def = lyr.GetDefinition() as CIMFeatureLayer;
                        dynamic cim = JsonConvert.DeserializeObject(def.ToJson());
                        try
                        {
                            this._timeField = cim.featureTable.timeFields.startTimeField;
                        }
                        catch
                        {
                            var msgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("ERROR: Time must be enabled on selected dateset to use the Track Viewer. " +
                                "Time is enabled in the item properties pane.");
                            _selectedField = null;
                            this.NotifyPropertyChanged("SelectedField");
                            
                        }

                        if (this._timeField != null)
                        {
                            using (RowCursor rowCursor = fc.Search(query))
                            {
                                ind = rowCursor.FindField(value.ToString());
                                while (rowCursor.MoveNext())
                                {
                                    using (Row row = rowCursor.Current)
                                    {
                                        Feature feat = row as Feature;
                                        var uid = feat.GetOriginalValue(ind);
                                        if (uids.IndexOf(uid.ToString()) == -1)
                                        {
                                            uids.Add(uid.ToString());
                                            //Queued Task preprocesses tracks into a Track object for data storage
                                            QueuedTask.Run(() =>
                                            {
                                                //handle int as the uid type as well
                                                if (uid is string)
                                                {
                                                    query.WhereClause = this.SelectedField + " = '" + uid + "'";
                                                }
                                                else
                                                {
                                                    query.WhereClause = this.SelectedField + " = " + uid;
                                                }
                                                Selection sel = lyr.Select(query);
                                                try
                                                {
                                                    _trackAttrs.Add(uid.ToString(), new TrackViewerDockpane.Track(sel, uid.ToString(), this._timeField));
                                                }
                                                catch
                                                {
                                                    failedTracks.Add(uid.ToString());
                                                }
                                                map.SetSelection(null);
                                            });
                                        }
                                    }
                                }
                            }
                            pd.Dispose();
                        }
                        if(failedTracks.Count > 0)
                        {
                            string msg = "The following tracks were unable to be added: " + string.Join(",", failedTracks);
                            var msgBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(msg);
                        }
                        this.TrackUIDs = uids;
                        this.NotifyPropertyChanged("TrackUIDs");
                    });
                    //execute queued task here to get UIDs
                }
            }
        }

        private string _selectedTrack;
        public string SelectedTrack
        {
            get { return _selectedTrack; }
            set
            {
                _selectedTrack = value;
                if (value != null) {

                    QueryFilter query = new QueryFilter();
                    Map map = MapView.Active.Map;
                    FeatureLayer lyr = this.SelectedFeatureLayer;
                    query.WhereClause = this.SelectedField + " = '" + _selectedTrack + "'";
                    QueuedTask.Run(() =>
                    {
                        var sel = lyr.Select(query);
                    });

                    TrackViewerDockpane.Track trk = new TrackViewerDockpane.Track();
                    ObservableCollection<object> attribute_values = new ObservableCollection<object>();
                    this._trackAttrs.TryGetValue(_selectedTrack, out trk);
                    this.TrackAttributes = trk.GetAttributeCollection();
                }
                else
                {
                    this.TrackAttributes = new ObservableCollection<string>();
                }
                this.NotifyPropertyChanged("TrackAttributes");
            }
        }

        private ObservableCollection<string> _trackAttributes = new ObservableCollection<string>();

        public ObservableCollection<string> TrackAttributes
        {
            get { return _trackAttributes; }

            set { _trackAttributes = value; }
        }

        private string _selectedSortMethod;

        public string SelectedSortMethod
        {
            get { return _selectedSortMethod;  }

            set
            {
                _selectedSortMethod = value;
                //this.SelectedTrack = null;

                ObservableCollection<string> uidsSorted = new ObservableCollection<string>();
                if (value == null)
                {
                }
                else if (value.EndsWith("Least Points"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderBy(key => key.Value.totalPoints))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if(value.EndsWith("Most Points"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderByDescending(key => key.Value.totalPoints))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if (value.EndsWith("Shortest Distance"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderBy(key => key.Value.totalDistance))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if (value.EndsWith("Longest Distance"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderByDescending(key => key.Value.totalDistance))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if(value.EndsWith("Shortest Time"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderBy(key => key.Value.totalTime))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if(value.EndsWith("Longest Time"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderByDescending(key => key.Value.totalTime))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                else if(value.EndsWith("Highest Average Speed"))
                {
                    foreach (KeyValuePair<string, TrackViewerDockpane.Track> pr in _trackAttrs.OrderByDescending(key => key.Value.avgSpeed))
                    {
                        uidsSorted.Add(pr.Key.ToString());
                    }
                }
                this.TrackUIDs = uidsSorted;
                this.NotifyPropertyChanged("TrackUIDs");
            }
        }

       
    }

    //TODO: sort by length? by time? by other characteristics?
    //TODO: attributes should be pre-computed to some degree....
    //TODO: hot link make layer from selected features tool 
    //TODO: add sort by std dev of heading to sort method

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class TrackViewerDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            TrackViewerDockpaneViewModel.Show();
        }
    }
}
