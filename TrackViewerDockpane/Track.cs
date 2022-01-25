using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProTrackViewer.TrackViewerDockpane
{
    class Track
    {
        /*The Track class handles the logic of processing track attributes and
         allows for continuous monitoring and update of real-time tracks*/
        public string uid;
        public int totalPoints;
        public double totalDistance;
        public TimeSpan totalTime;
        public DateTime startTime;
        public DateTime endTime;
        public double avgSpeed;
        public double lastSpeed;
        public double lastHeading;
        public string overallDirection;
        public object firstPoint;
        public object lastPoint;

        public Track()
        {

        }

        public Track(Selection selection, string trackUID, string timefield)
        {
            double totalDist = 0.0;
            int timeInd;
            DateTime startTime = new DateTime();
            DateTime endTime = new DateTime();
            DateTime timestamp = new DateTime();
            DateTime prevTime = new DateTime();
            double totalSpeed = 0.0;
            double totalSpeedCount = 0;
            Geometry currentPoint = null;
            Geometry lastPoint = null;
            Geometry firstPoint = null;
            Row row = null;
            Feature feat = null;
            double angle12 = 0.0;
            double angle21 = 0.0;
            double overallAngle = 0.0;
            double speed = 0.0;
            double dist = 0.0;

            using (RowCursor rowCursor = selection.Search())
            {
                rowCursor.MoveNext();
                using (row = rowCursor.Current)
                {
                    timeInd = rowCursor.FindField(timefield);

                    feat = row as Feature;
                    lastPoint = feat.GetShape();
                    this.firstPoint = lastPoint;

                    firstPoint = lastPoint;
                    //TODO: error handle for no timefield
                    if (lastPoint != null)
                    {
                        startTime = (DateTime)feat.GetOriginalValue(timeInd);
                        endTime = startTime;
                        prevTime = startTime;
                    }

                    rowCursor.MoveNext();
                }
                while (rowCursor.MoveNext())
                {
                    using (row = rowCursor.Current)
                    {
                        feat = row as Feature;

                        currentPoint = feat.GetShape();

                        if (currentPoint != null)
                        {
                            timestamp = (DateTime)feat.GetOriginalValue(timeInd);
                            if (timestamp < startTime) { startTime = timestamp; }
                            else if (timestamp > endTime) { endTime = timestamp; }

                            dist = GeometryEngine.Instance.GeodesicDistance(lastPoint, currentPoint);
                            speed = dist / (timestamp - prevTime).TotalSeconds;
                            totalDist += dist;
                            totalSpeed += speed;
                            totalSpeedCount += 1;
                            lastPoint = currentPoint;
                            prevTime = timestamp;
                        }
                    }
                }
                this.lastPoint = currentPoint;
                var _ = GeometryEngine.Instance.GeodeticDistanceAndAzimuth(lastPoint as MapPoint, currentPoint as MapPoint, GeodeticCurveType.Geodesic, out angle12, out angle21);
                _ = GeometryEngine.Instance.GeodeticDistanceAndAzimuth(firstPoint as MapPoint, lastPoint as MapPoint, GeodeticCurveType.Geodesic, out overallAngle, out angle21);
            }
            this.uid = trackUID;
            this.totalPoints = selection.GetCount();
            this.totalDistance = Math.Round(totalDist, 2);
            this.totalTime = (endTime - startTime);
            this.startTime = startTime;
            this.endTime = endTime;
            this.avgSpeed = Math.Round((totalSpeed / totalSpeedCount), 2);
            this.lastSpeed = Math.Round(speed, 2);
            this.lastHeading = Math.Round(angle12, 2);
            this.overallDirection = GetDirection(Math.Round(overallAngle, 2));

        }
        public string GetDirection(double heading)
        {
            int val = (int)Math.Floor((heading / 22.5) + 0.5);
            string[] dirs = { "N", "N-NE", "NE", "E-NE", "E", "E-SE", "SE", "S-SE", "S", "S-SW", "SW", "W-SW", "W", "W-NW", "NW", "N-NW", "N" };
            return dirs[val];
        }

        public ObservableCollection<string> GetAttributeCollection()
        {
            ObservableCollection<string> attributes = new ObservableCollection<string>();
            attributes.Add("UID: " + this.uid);
            attributes.Add("Total Points: " + this.totalPoints.ToString());
            attributes.Add("Total Distance (m): " + this.totalDistance.ToString());
            attributes.Add("Total Time: " + this.totalTime.ToString());
            attributes.Add("Start Time: " + this.startTime.ToString());
            attributes.Add("End Time: " + this.endTime.ToString());
            attributes.Add("Avg Speed (m/s): " + this.avgSpeed.ToString());
            attributes.Add("Last Speed (m/s): " + this.lastSpeed.ToString());
            attributes.Add("Last Heading: " + this.lastHeading.ToString());
            attributes.Add("Overall Direction: " + this.overallDirection);
            return attributes;
        }

        public void UpdateTrack()
        {
            //take new point and time
            //update last heading, endtime, totaltime, etc...
        }
    }
}
