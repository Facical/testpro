using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace testpro.Models
{
    public class Room
    {

        public List<Wall> Walls { get; set; } = new List<Wall>();
        public Brush FloorBrush { get; set; }
        public string Name { get; set; }
        public double Area { get; private set; }

        // 3D 렌더링을 위한 추가 속성
        public Color FloorColor { get; private set; }
        public bool IsVisible { get; set; } = true;

        public Room(string name = "Room")
        {
            Name = name;
            SetFloorColor(); // 메서드 이름 변경
        }

        private void SetFloorColor()
        {
            // 고정된 옅은 회색 사용
            FloorColor = Color.FromArgb(50, 200, 200, 200); // 알파값 50으로 투명하게

            // FloorBrush 생성 (2D용)
            FloorBrush = new SolidColorBrush(FloorColor);
        }

        public void SetCustomFloorColor(Color color)
        {
            FloorColor = color;
            var opaqueColor = Color.FromArgb(200, color.R, color.G, color.B);
            FloorBrush = new SolidColorBrush(opaqueColor);
        }

        public void AddWall(Wall wall)
        {
            Walls.Add(wall);
            CalculateArea();
        }

        public bool IsClosedRoom()
        {
            if (Walls.Count < 3) return false;

            // Check if all walls form a closed loop
            var points = new List<Point2D>();
            foreach (var wall in Walls)
            {
                points.Add(wall.StartPoint);
                points.Add(wall.EndPoint);
            }

            // Check if we can form a closed path
            var visited = new HashSet<Point2D>();
            var path = new List<Point2D>();

            if (TryBuildClosedPath(points, path, visited, Walls.First().StartPoint))
            {
                return path.Count >= 3;
            }

            return false;
        }

        private bool TryBuildClosedPath(List<Point2D> allPoints, List<Point2D> path, HashSet<Point2D> visited, Point2D start)
        {
            if (path.Count > 0 && start.Equals(path.First()) && path.Count >= 3)
            {
                return true;
            }

            if (visited.Contains(start)) return false;

            visited.Add(start);
            path.Add(start);

            foreach (var wall in Walls)
            {
                Point2D next = null;
                if (wall.StartPoint.Equals(start))
                    next = wall.EndPoint;
                else if (wall.EndPoint.Equals(start))
                    next = wall.StartPoint;

                if (next != null)
                {
                    if (TryBuildClosedPath(allPoints, path, visited, next))
                        return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            visited.Remove(start);
            return false;
        }

        private void CalculateArea()
        {
            if (!IsClosedRoom())
            {
                Area = 0;
                return;
            }

            // Shoelace formula for polygon area (개선된 버전)
            var points = GetOrderedPoints();
            if (points.Count < 3)
            {
                Area = 0;
                return;
            }

            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int j = (i + 1) % points.Count;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            Area = Math.Abs(area) / 2.0;

            // 면적이 계산되면 방 이름 업데이트
            UpdateRoomName();
        }

        private void UpdateRoomName()
        {
            if (Area > 0)
            {
                var areaInSqFt = Area / 144.0; // 제곱인치를 제곱피트로
                Name = $"Room ({areaInSqFt:F1} ft²)";
            }
        }

        private List<Point2D> GetOrderedPoints()
        {
            var points = new List<Point2D>();
            if (Walls.Count == 0) return points;

            var start = Walls.First().StartPoint;
            points.Add(start);

            var current = start;
            var usedWalls = new HashSet<Wall>();

            while (usedWalls.Count < Walls.Count)
            {
                Wall nextWall = null;
                Point2D nextPoint = null;

                foreach (var wall in Walls.Where(w => !usedWalls.Contains(w)))
                {
                    if (wall.StartPoint.Equals(current))
                    {
                        nextWall = wall;
                        nextPoint = wall.EndPoint;
                        break;
                    }
                    else if (wall.EndPoint.Equals(current))
                    {
                        nextWall = wall;
                        nextPoint = wall.StartPoint;
                        break;
                    }
                }

                if (nextWall == null) break;

                usedWalls.Add(nextWall);
                if (!nextPoint.Equals(start))
                {
                    points.Add(nextPoint);
                    current = nextPoint;
                }
                else
                {
                    break;
                }
            }

            return points;
        }

        // 방의 중심점 계산 (3D 카메라 포커싱용)
        public Point2D GetCenterPoint()
        {
            var points = GetOrderedPoints();
            if (points.Count == 0) return new Point2D(0, 0);

            var centerX = points.Average(p => p.X);
            var centerY = points.Average(p => p.Y);
            return new Point2D(centerX, centerY);
        }

        // 방의 경계 상자 계산
        public (Point2D min, Point2D max) GetBoundingBox()
        {
            var points = GetOrderedPoints();
            if (points.Count == 0)
                return (new Point2D(0, 0), new Point2D(0, 0));

            var minX = points.Min(p => p.X);
            var maxX = points.Max(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);

            return (new Point2D(minX, minY), new Point2D(maxX, maxY));
        }

        // 디버그 정보
        public override string ToString()
        {
            return $"{Name} - {Walls.Count} walls, {Area:F1} sq.in., Closed: {IsClosedRoom()}";
        }
    }
}