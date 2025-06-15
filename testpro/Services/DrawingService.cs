using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using testpro.Models;

namespace testpro.Services
{
    public class DrawingService : INotifyPropertyChanged
    {
        public List<Wall> Walls { get; } = new List<Wall>();
        public List<Room> Rooms { get; } = new List<Room>();
        public List<StoreObject> StoreObjects { get; } = new List<StoreObject>();

        private const double SnapDistance = 10.0;

        private double _scaleX = 1.0;
        private double _scaleY = 1.0;

        public double ScaleX
        {
            get => _scaleX;
            private set { _scaleX = value > 0 ? value : 1.0; OnPropertyChanged(); }
        }

        public double ScaleY
        {
            get => _scaleY;
            private set { _scaleY = value > 0 ? value : 1.0; OnPropertyChanged(); }
        }

        public double Scale
        {
            get => (_scaleX + _scaleY) / 2.0;
            private set { ScaleX = value; ScaleY = value; }
        }

        public string BackgroundImagePath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetScale(double scale) => Scale = scale;
        public void SetScaleXY(double scaleX, double scaleY) { ScaleX = scaleX; ScaleY = scaleY; }

        public Point2D SnapToExistingPoint(Point2D point)
        {
            foreach (var wall in Walls)
            {
                if (point.DistanceTo(wall.StartPoint) < SnapDistance) return wall.StartPoint;
                if (point.DistanceTo(wall.EndPoint) < SnapDistance) return wall.EndPoint;
            }
            return point;
        }

        public Wall AddWall(Point2D startPoint, Point2D endPoint)
        {
            var wall = new Wall(SnapToExistingPoint(startPoint), SnapToExistingPoint(endPoint));
            Walls.Add(wall);
            UpdateRooms();
            NotifyChanged();
            return wall;
        }

        // 기존 2개 인자를 받는 메서드
        public StoreObject AddStoreObject(ObjectType type, Point2D position)
        {
            var obj = new StoreObject(type, position);
            StoreObjects.Add(obj);
            NotifyChanged();
            return obj;
        }

        // === 오류 해결을 위해 추가된 메서드 1 ===
        // 4개의 인자를 받는 메서드 (객체 그리기용)
        public StoreObject AddStoreObject(ObjectType type, Point2D position, double width, double height)
        {
            var obj = new StoreObject(type, position)
            {
                Width = width,
                Length = height // 2D 캔버스에서는 Height가 Length(깊이)를 의미
            };
            StoreObjects.Add(obj);
            NotifyChanged();
            return obj;
        }

        // === 오류 해결을 위해 추가된 메서드 2 ===
        // 9개의 인자를 받는 메서드 (객체 붙여넣기용)
        public StoreObject AddStoreObject(ObjectType type, Point2D position, double width, double length, double height, int layers, bool isHorizontal, double temperature, string categoryCode)
        {
            var obj = new StoreObject(type, position)
            {
                Width = width,
                Length = length,
                Height = height,
                Layers = layers,
                IsHorizontal = isHorizontal,
                Temperature = temperature,
                CategoryCode = categoryCode
            };
            StoreObjects.Add(obj);
            NotifyChanged();
            return obj;
        }

        public void RemoveStoreObject(StoreObject obj)
        {
            StoreObjects.Remove(obj);
            NotifyChanged();
        }

        public void UpdateStoreObject(StoreObject obj, double width, double length, double height, int layers, bool isHorizontal, double temperature, string categoryCode)
        {
            obj.Width = width;
            obj.Length = length;
            obj.Height = height;
            obj.Layers = layers;
            obj.IsHorizontal = isHorizontal;
            obj.Temperature = temperature;
            obj.CategoryCode = categoryCode;
            obj.Rotation = isHorizontal ? 0 : 90;
            obj.ModifiedAt = DateTime.Now;

            NotifyChanged();
        }

        public StoreObject GetObjectAt(Point2D point)
        {
            return StoreObjects.LastOrDefault(obj => obj.ContainsPoint(point));
        }

        public void Clear()
        {
            Walls.Clear();
            Rooms.Clear();
            StoreObjects.Clear();
            BackgroundImagePath = null;
            ScaleX = 1.0;
            ScaleY = 1.0;
            NotifyChanged();
        }

        private void UpdateRooms()
        {
            Rooms.Clear();
            var processedWalls = new HashSet<Wall>();
            foreach (var wall in Walls)
            {
                if (processedWalls.Contains(wall)) continue;
                var room = TryCreateRoomFromWall(wall, processedWalls);
                if (room != null && room.IsClosedRoom() && room.Area > 100)
                {
                    Rooms.Add(room);
                }
            }
        }

        private Room TryCreateRoomFromWall(Wall startWall, HashSet<Wall> processedWalls)
        {
            var room = new Room();
            var currentWalls = new List<Wall>();
            var visited = new HashSet<Wall>();
            if (FindConnectedWalls(startWall, currentWalls, visited))
            {
                foreach (var wall in currentWalls)
                {
                    room.AddWall(wall);
                    processedWalls.Add(wall);
                }
                return room;
            }
            return null;
        }

        private bool FindConnectedWalls(Wall startWall, List<Wall> roomWalls, HashSet<Wall> visited)
        {
            if (visited.Contains(startWall)) return false;
            visited.Add(startWall);
            roomWalls.Add(startWall);

            foreach (var connected in GetConnectedWalls(startWall))
            {
                if (!visited.Contains(connected))
                {
                    if (FindConnectedWalls(connected, roomWalls, visited)) return true;
                }
                else if (connected == roomWalls.First() && roomWalls.Count >= 3)
                {
                    return true;
                }
            }
            return false;
        }

        private List<Wall> GetConnectedWalls(Wall wall)
        {
            return Walls.Where(other => other != wall && wall.IsConnectedTo(other)).ToList();
        }

        private void NotifyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}